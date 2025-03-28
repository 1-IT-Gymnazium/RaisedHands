namespace RaisedHands.Api.Models.Groups;

public class UserRoleGroupModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}
