using System.ComponentModel.DataAnnotations;
namespace RaisedHands.Api.Models.Auth;

public class RegisterModel
{

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string FirstName { get; set; } = null!;
    [Required]
    public string LastName { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}
