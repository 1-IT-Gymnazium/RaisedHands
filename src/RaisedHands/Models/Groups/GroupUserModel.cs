namespace RaisedHands.Api.Models.Groups;

public class GroupUserModel
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = null!;
}
