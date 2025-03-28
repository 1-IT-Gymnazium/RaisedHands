using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Users;

public class UserInfoModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Email { get; set; }
}

public static class UserInfoModelExtensions
{
    public static UserInfoModel ToUserInfo(this User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email
    };
}

