using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Users;

public class ChangePasswordModel
{
    [Required]
    public string OldPassword { get; set; } = null!;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } =  null!;

}
