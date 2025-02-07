using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using RaisedHands.Api.Models.Auth;
using RaisedHands.Api.Services;
using RaisedHands.Api.Settings;
using RaisedHands.Api.Utils;
using RaisedHands.Data;
using RaisedHands.Data.Entities;
using RaisedHands.Data.Entities.Identity;
using RaisedHands.Data.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RaisedHands.Api.Controllers;
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IClock _clock;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly AppDbContext _dbContext;
    private readonly EmailSenderService _emailSenderService;
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        IClock clock,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        AppDbContext dbContext,
        EmailSenderService emailSenderService,
        IOptions<JwtSettings> options)
    {
        _clock = clock;
        _dbContext = dbContext;
        _signInManager = signInManager;
        _userManager = userManager;
        _emailSenderService = emailSenderService;
        _jwtSettings = options.Value;
    }

    // We will also add verion of endpoint into post controller
    [HttpPost("api/v1/Auth/Register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Register(
       [FromBody] RegisterModel model
       )
    {
        var validator = new PasswordValidator<User>();
        var now = _clock.GetCurrentInstant();

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = model.Email,
            UserName = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
        }.SetCreateBySystem(now);

        var checkPassword = await validator.ValidateAsync(_userManager, newUser, model.Password);

        if (!checkPassword.Succeeded)
        {
            ModelState.AddModelError<RegisterModel>(
                x => x.Password, string.Join("\n", checkPassword.Errors.Select(x => x.Description)));
            return ValidationProblem(ModelState);
        }

        // Method with SaveChanges()!
        var result = await _userManager.CreateAsync(newUser);
        // Method with SaveChanges()!
        await _userManager.AddPasswordAsync(newUser, model.Password);

        var token = string.Empty;
        token = await _userManager.GenerateEmailConfirmationTokenAsync(newUser);

        await _emailSenderService.AddEmailToSendAsync(
            model.Email,
            "Potvrzení registrace",
            $"<a href=\"https://www.projectmanagement.cz/?token={Uri.EscapeDataString(token)}&email={model.Email}\">{token}</a>"
            );
        return Ok(token);
    }

    [HttpPost("api/v1/Auth/Login")]
    public async Task<ActionResult> Login([FromBody] LoginModel model)
    {
        var normalizedEmail = model.Email.ToUpperInvariant();
        var user = await _userManager
            .Users
            .SingleOrDefaultAsync(x => x.EmailConfirmed && x.NormalizedEmail == normalizedEmail)
            ;

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "LOGIN_FAILED");
            return ValidationProblem(ModelState);
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "LOGIN_FAILED");
            return ValidationProblem(ModelState);
        }

        var accessToken = GenerateAccessToken(user.Id, model.Email, user.UserName!, _jwtSettings.AccessTokenExpirationInMinutes);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, _jwtSettings.RefreshTokenExpirationInDays);
        Response.Cookies.Append("RefreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // For HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationInDays)
        });
        return Ok(new { Token = accessToken });
    }

    /// <summary>
    /// unescape token before sending
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/v1/Auth/ValidateToken")]
    public async Task<ActionResult> ValidateToken(
        [FromBody] TokenModel model
        )
    {
        var normalizedMail = model.Email.ToUpperInvariant();
        var user = await _userManager
            .Users
            .SingleOrDefaultAsync(x => !x.EmailConfirmed && x.NormalizedEmail == normalizedMail);

        if (user == null)
        {
            ModelState.AddModelError<TokenModel>(x => x.Token, "INVALID_TOKEN");
            return ValidationProblem(ModelState);
        }

        var check = await _userManager.ConfirmEmailAsync(user, model.Token);
        if (!check.Succeeded)
        {
            ModelState.AddModelError<TokenModel>(x => x.Token, "INVALID_TOKEN");
            return ValidationProblem(ModelState);
        }

        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("api/v1/Auth/UserInfo")]
    public async Task<ActionResult<LoggedUserModel>> GetUserInfo()
    {
        if (!User.Identities.Any(x => x.IsAuthenticated))
        {
            return new LoggedUserModel
            {
                id = default,
                name = null,
                email = null,
                isAuthenticated = false,
            };
        }

        var id = User.GetUserId();
        var user = await _userManager.Users
            .Where(x => x.Id == id)
            .AsNoTracking()
            .SingleAsync();

        var loggedModel = new LoggedUserModel
        {
            id = user.Id,
            name = user.UserName,
            isAuthenticated = true,
            email = null,
        };

        return loggedModel;
    }
    [HttpPost("api/v1/Auth/Refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out var incomingToken))
        {
            return Unauthorized(new { Message = "Refresh token not found" });
        }
        var hashedToken = Hash(incomingToken);
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hashedToken);
        if (storedToken == null || storedToken.ExpiresAt < _clock.GetCurrentInstant() || storedToken.RevokedAt != null)
        {
            return Unauthorized(new { Message = "Invalid or expired refresh token" });
        }
        // Generate new access and refresh tokens
        var user = await _dbContext.Users.FindAsync(storedToken.UserId);
        if (user == null)
        {
            return Unauthorized();
        }
        // Generate new tokens
        var newAccessToken = GenerateAccessToken(user.Id, user.Email!, user.UserName!, _jwtSettings.AccessTokenExpirationInMinutes);
        var newRefreshToken = await GenerateRefreshTokenAsync(user.Id, _jwtSettings.RefreshTokenExpirationInDays);
        storedToken.RevokedAt = _clock.GetCurrentInstant();
        await _dbContext.SaveChangesAsync();
        Response.Cookies.Append("RefreshToken", newRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // For HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationInDays)
        });
        return Ok(new
        {
            Token = newAccessToken,
        });
    }

    [Authorize]
    [HttpPost("api/v1/Auth/Logout")]
    public async Task<ActionResult> Logout()
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out var incomingToken))
        {
            return NoContent();
        }
        var hashedToken = Hash(incomingToken);
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hashedToken);
        if (storedToken == null || storedToken.ExpiresAt < _clock.GetCurrentInstant() || storedToken.RevokedAt != null)
        {
            return NoContent();
        }
        storedToken.ExpiresAt = _clock.GetCurrentInstant();
        await _dbContext.SaveChangesAsync();
        Response.Cookies.Delete("RefreshToken");
        return NoContent();
    }

    [Authorize]
    [HttpGet("api/v1/Auth/TestMeBeforeLoginAndAfter")]
    public ActionResult TestMeBeforeLoginAndAfter()
    {
        return Ok("Succesfully reached endpoint!");
    }

    private async Task<string> GenerateRefreshTokenAsync(Guid userId, int expirationInDays)
    {
        var refreshToken = Guid.NewGuid().ToString();
        var data = Request.Headers.UserAgent.ToString();
        var now = _clock.GetCurrentInstant();
        _dbContext.Add(new RefreshToken
        {
            UserId = userId,
            Token = Hash(refreshToken),
            CreatedAt = now,
            ExpiresAt = now.Plus(Duration.FromDays(expirationInDays)),
            RequestInfo = data,
        });
        await _dbContext.SaveChangesAsync();
        return refreshToken;
    }
    private string GenerateAccessToken(Guid userId, string email, string username, int expirationInMinutes)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, username)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(expirationInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public static string Hash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
