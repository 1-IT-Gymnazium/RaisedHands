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

    /// <summary>
    /// Handles the forgot password request.
    /// </summary>
    /// <param name="model">The model containing the email of the user who wants to reset their password.</param>
    /// <returns>An HTTP response indicating success or failure.</returns>
    [HttpPost("api/v1/Auth/ForgotPassword")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError<ForgotPasswordModel>(
                x => x.Email, "If your email is registered, you will receive a password reset link.");
            return ValidationProblem(ModelState);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        await _emailSenderService.AddEmailToSendAsync(
            model.Email,
            "Reset Your Password",
            $@"
<html>
<head>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 40px auto;
            background: #ffffff;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0px 2px 10px rgba(0, 0, 0, 0.1);
            text-align: center;
        }}
        h2 {{
            color: #333;
        }}
        p {{
            color: #555;
            font-size: 16px;
        }}
        .button {{
            display: inline-block;
            background-color: #007bff;
            color: #ffffff;
            text-decoration: none;
            padding: 12px 20px;
            border-radius: 5px;
            font-size: 16px;
            margin-top: 20px;
        }}
        .button:hover {{
            background-color: #0056b3;
        }}
        .footer {{
            margin-top: 20px;
            font-size: 12px;
            color: #777;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Reset Your Password</h2>
        <p>You have requested to reset your password. Click the button below to proceed:</p>
        <a class=""button"" href=""http://localhost:4200/reset-password?token={{Uri.EscapeDataString(token)}}&email={{model.Email}}"">
            Reset Password
        </a>
        <p class=""footer"">If you did not request this, please ignore this email.</p>
    </div>
</body>
</html>
"
        );

        return Ok();
    }

    /// <summary>
    /// Handles the reset password request.
    /// </summary>
    /// <param name="model">The model containing email, token, and new password.</param>
    /// <returns>An HTTP response indicating success or failure.</returns>
    [HttpPost("api/v1/Auth/ResetPassword")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordModel model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError<ResetPasswordModel>(
                x => x.Email, "Invalid email or token.");
            return ValidationProblem(ModelState);
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (!resetResult.Succeeded)
        {
            ModelState.AddModelError<ResetPasswordModel>(
                x => x.NewPassword, string.Join("\n", resetResult.Errors.Select(e => e.Description)));
            return ValidationProblem(ModelState);
        }

        return Ok();
    }

    /// <summary>
    /// Registers a new user and sends an email confirmation link.
    /// </summary>
    /// <param name="model">The registration details including email, name, and password.</param>
    /// <returns>HTTP 200 if successful, validation errors otherwise.</returns>
    [HttpPost("api/v1/Auth/Register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Register([FromBody] RegisterModel model)
    {
        var validator = new PasswordValidator<User>();
        var now = _clock.GetCurrentInstant();

        // Check if email is already registered
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "Email is already in use.");
            return ValidationProblem(ModelState);
        }

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
            ModelState.AddModelError("Password", string.Join("\n", checkPassword.Errors.Select(x => x.Description)));
            return ValidationProblem(ModelState);
        }

        // Create user and check if creation is successful
        var result = await _userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("UserCreation", "Failed to create user. Please try again.");
            return ValidationProblem(ModelState);
        }

        await _userManager.AddPasswordAsync(newUser, model.Password);
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(newUser);

        await _emailSenderService.AddEmailToSendAsync(
            model.Email,
            "Potvrzení registrace",
            $@"
    <html>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{
            width: 100%;
            max-width: 600px;
            margin: 20px auto;
            background-color: #ffffff;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0px 4px 8px rgba(0, 0, 0, 0.1);
            text-align: center;
        }}
        h2 {{
            color: #333;
        }}
        p {{
            color: #666;
            font-size: 16px;
        }}
        .btn {{
            display: inline-block;
            background-color: #007bff;
            color: #ffffff;
            padding: 12px 20px;
            text-decoration: none;
            font-size: 16px;
            border-radius: 5px;
            margin-top: 20px;
        }}
        .btn:hover {{
            background-color: #0056b3;
        }}
        .footer {{
            margin-top: 20px;
            font-size: 12px;
            color: #999;
        }}
    </style>
    <body>
<div class=""container"">
        <h2>Confirm Your Registration</h2>
        <p>Click the button below to confirm your email:</p>
        <a class=""btn"" href='http://localhost:4200/confirm?token={Uri.EscapeDataString(token)}&email={model.Email}'>
            Confirm Email
        </a>
        <p class=""footer"">If you did not request this registration, please ignore this email.</p>
    </div>
    </body>
    </html>"
        );

        return Ok();
    }

    /// <summary>
    /// Authenticates a user and returns an access token.
    /// </summary>
    /// <param name="model">The login credentials.</param>
    /// <returns>JWT token if successful, validation error otherwise.</returns>
    [HttpPost("api/v1/Auth/Login")]
    public async Task<ActionResult> Login([FromBody] LoginModel model)
    {
        var normalizedEmail = model.Email.ToUpperInvariant();
        var user = await _userManager.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

        if (user == null || !user.EmailConfirmed)
        {
            ModelState.AddModelError("error", user == null ? "LOGIN_FAILED" : "EMAIL_NOT_VERIFIED");
            return ValidationProblem(ModelState);
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError("error", "LOGIN_FAILED");
            return ValidationProblem(ModelState);
        }

        var accessToken = GenerateAccessToken(user.Id, model.Email, user.UserName!, _jwtSettings.AccessTokenExpirationInMinutes);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, _jwtSettings.RefreshTokenExpirationInDays);

        Response.Cookies.Append("RefreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationInDays)
        });

        return Ok(new { Token = accessToken });
    }

    /// <summary>
    /// Validates a token for email confirmation.
    /// </summary>
    /// <param name="model">The token and email for validation.</param>
    /// <returns>HTTP 204 if successful, validation errors otherwise.</returns>
    [HttpPost("api/v1/Auth/ValidateToken")]
    public async Task<ActionResult> ValidateToken([FromBody] TokenModel model)
    {
        var normalizedMail = model.Email.ToUpperInvariant();
        var user = await _userManager.Users.SingleOrDefaultAsync(x => !x.EmailConfirmed && x.NormalizedEmail == normalizedMail);

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

    /// <summary>
    /// Retrieves authenticated user's information.
    /// </summary>
    /// <returns>User details if authenticated, otherwise default values.</returns>
    [AllowAnonymous]
    [HttpGet("api/v1/Auth/UserInfo")]
    public async Task<ActionResult<LoggedUserModel>> GetUserInfo()
    {
        if (!User.Identities.Any(x => x.IsAuthenticated))
        {
            return new LoggedUserModel { id = default, name = null, email = null, isAuthenticated = false };
        }

        var id = User.GetUserId();
        var user = await _userManager.Users.Where(x => x.Id == id).AsNoTracking().SingleAsync();

        return new LoggedUserModel { id = user.Id, name = user.UserName, isAuthenticated = true, email = user.Email };
    }

    /// <summary>
    /// Refreshes the access token using a refresh token.
    /// </summary>
    /// <returns>New access token if successful, Unauthorized otherwise.</returns>
    [HttpPost("api/v1/Auth/Refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out var incomingToken))
        {
            return Unauthorized(new { Message = "Refresh token not found" });
        }

        var hashedToken = Hash(incomingToken);
        var storedToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == hashedToken);

        if (storedToken == null || storedToken.ExpiresAt < _clock.GetCurrentInstant() || storedToken.RevokedAt != null)
        {
            return Unauthorized(new { Message = "Invalid or expired refresh token" });
        }

        var user = await _dbContext.Users.FindAsync(storedToken.UserId);
        if (user == null) return Unauthorized();

        var newAccessToken = GenerateAccessToken(user.Id, user.Email!, user.UserName!, _jwtSettings.AccessTokenExpirationInMinutes);
        var newRefreshToken = await GenerateRefreshTokenAsync(user.Id, _jwtSettings.RefreshTokenExpirationInDays);

        storedToken.RevokedAt = _clock.GetCurrentInstant();
        await _dbContext.SaveChangesAsync();

        Response.Cookies.Append("RefreshToken", newRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationInDays)
        });

        return Ok(new { Token = newAccessToken });
    }

    /// <summary>
    /// Logs out a user by revoking their refresh token.
    /// </summary>
    /// <returns>HTTP 204 on success.</returns>
    [Authorize]
    [HttpPost("api/v1/Auth/Logout")]
    public async Task<ActionResult> Logout()
    {
        if (!Request.Cookies.TryGetValue("RefreshToken", out var incomingToken))
        {
            return NoContent();
        }

        var hashedToken = Hash(incomingToken);
        var storedToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == hashedToken);

        if (storedToken == null || storedToken.ExpiresAt < _clock.GetCurrentInstant() || storedToken.RevokedAt != null)
        {
            return NoContent();
        }

        storedToken.ExpiresAt = _clock.GetCurrentInstant();
        await _dbContext.SaveChangesAsync();
        Response.Cookies.Delete("RefreshToken");

        return NoContent();
    }

    /// <summary>
    /// Tests authentication by returning a success message.
    /// </summary>
    /// <returns>Success message if authenticated.</returns>
    [Authorize]
    [HttpGet("api/v1/Auth/TestMeBeforeLoginAndAfter")]
    public ActionResult TestMeBeforeLoginAndAfter()
    {
        return Ok("Successfully reached endpoint!");
    }

    /// <summary>
    /// Generates a refresh token for a user and stores it in the database.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="expirationInDays">Number of days before the refresh token expires.</param>
    /// <returns>A newly generated refresh token as a string.</returns>
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

    /// <summary>
    /// Generates a JWT access token for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="email">The email of the user.</param>
    /// <param name="username">The username of the user.</param>
    /// <param name="expirationInMinutes">The number of minutes before the token expires.</param>
    /// <returns>A signed JWT access token as a string.</returns>
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
            expires: DateTime.UtcNow.AddMinutes(expirationInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Hashes a token using SHA-256 for secure storage.
    /// </summary>
    /// <param name="token">The token to be hashed.</param>
    /// <returns>A base64-encoded SHA-256 hash of the input token.</returns>
    public static string Hash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
