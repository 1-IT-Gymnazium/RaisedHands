using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Auth;

public class TokenModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
    [Required]
    public string Token { get; set; } = null!;
}
