using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Groups;

public class GroupUserModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = null!;
}

public static class GroupUserModelExtensions
{
    public static GroupUserModel ToGroupUserModel(this UserRoleGroup source)
        => new()
        {
            Id = source.UserRole.User.Id,
            FirstName = source.UserRole.User.FirstName,
            LastName = source.UserRole.User.LastName,
            RoleId = source.UserRole.Role.Id,
            RoleName = source.UserRole.Role.Name = null!
        };
}

