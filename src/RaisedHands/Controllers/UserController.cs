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
    private readonly UserManager<User> _userManager;

    public UserController(
        AppDbContext dbContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager
        )
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Retrieves the currently authenticated user's profile information.
    /// </summary>
    /// <remarks>
    /// The user must be logged in and authenticated via JWT or cookie.
    /// </remarks>
    /// <returns>
    /// 200 OK with <see cref="UserInfoModel"/> if successful.<br/>
    /// 401 Unauthorized if the user is not authenticated.<br/>
    /// 404 Not Found if the user does not exist in the system.
    /// </returns>
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
                .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(user.ToUserInfo());
    }

    /// <summary>
    /// Updates the profile details of the currently authenticated user, excluding their password.
    /// </summary>
    /// <remarks>
    /// Only fields such as first name, last name, and email can be updated. If the email is changed, the username will also be updated.
    /// </remarks>
    /// <param name="model">An instance of <see cref="UpdateUserModel"/> containing the new user data.</param>
    /// <returns>
    /// 200 OK if the update is successful.<br/>
    /// 401 Unauthorized if the user is not authenticated.<br/>
    /// 404 Not Found if the user does not exist.<br/>
    /// 400 Bad Request if the update fails validation or saving.
    /// </returns>
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

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Email = model.Email;

        if (emailChanged)
        {
            user.UserName = model.Email;
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to update user info", errors = result.Errors });
        }

        return Ok(new { message = "User info updated successfully" });
    }

    /// <summary>
    /// Changes the password of the currently authenticated user.
    /// </summary>
    /// <remarks>
    /// The user must provide their current password along with a new password. The password will be changed only if the current one is verified successfully.
    /// </remarks>
    /// <param name="model">An instance of <see cref="ChangePasswordModel"/> containing the old and new password values.</param>
    /// <returns>
    /// 200 OK if the password is changed successfully.<br/>
    /// 401 Unauthorized if the user is not authenticated.<br/>
    /// 404 Not Found if the user does not exist.<br/>
    /// 400 Bad Request if the current password is incorrect or validation fails.
    /// </returns>
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

        var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Failed to change password", errors = result.Errors });
        }

        return Ok(new { message = "Password changed successfully" });
    }
}
