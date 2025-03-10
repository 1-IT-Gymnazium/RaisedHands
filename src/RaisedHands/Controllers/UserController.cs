using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaisedHands.Api.Models.Users;
using RaisedHands.Data;
using RaisedHands.Data.Entities;
using System.Security.Claims;

namespace RaisedHands.Api.Controllers;

public class UserController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public UserController(AppDbContext dbContext, UserManager<User> userManager, SignInManager<User> signInManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    /// <summary>
    /// Gets the currently authenticated user's information.
    /// </summary>
    [Authorize]
    [HttpGet("api/v1/User/UserInfo")]
    public async Task<ActionResult> GetUserInfo()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User is not authenticated" });
        }

        var user = await _userManager.Users
            .Where(x => x.Id.ToString() == userId)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.PhoneNumber
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(user);
    }

    /// <summary>
    /// Updates user profile information (except password).
    /// </summary>
    [Authorize]
    [HttpPatch("api/v1/User/Update")]
    public async Task<ActionResult> UpdateUserInfo([FromBody] UpdateUserModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User is not authenticated" });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        bool emailChanged = user.Email != model.Email;

        // Update user properties
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Email = model.Email;

        if (emailChanged)
        {
            user.UserName = model.Email; // ✅ Update username if email is changed
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to update user info", errors = result.Errors });
        }

        return Ok(new { message = "User info updated successfully" });
    }

    /// <summary>
    /// Changes the authenticated user's password.
    /// </summary>
    [Authorize]
    [HttpPost("api/v1/User/ChangePassword")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User is not authenticated" });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            return BadRequest(new { message = "New passwords do not match" });
        }

        var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to change password", errors = result.Errors });
        }

        return Ok(new { message = "Password changed successfully" });
    }
}
