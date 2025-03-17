using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Auth;

public class ResetPasswordModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = null!;
}

