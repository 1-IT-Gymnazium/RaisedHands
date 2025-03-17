namespace RaisedHands.Api.Models.Users;

public class ChangePasswordModel
{
    public string OldPassword { get; set; } = null!;
    public string NewPassword { get; set; } =  null!;
    public string ConfirmPassword { get; set; } = null!;
}
