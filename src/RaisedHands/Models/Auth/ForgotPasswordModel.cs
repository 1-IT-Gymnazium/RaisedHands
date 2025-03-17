using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Auth;

public class ForgotPasswordModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
}

