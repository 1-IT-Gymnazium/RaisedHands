using RaisedHands.Api.Models.Rooms;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Users;

public class UserDetailModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

}
public static class UserDetailModelExtensions
{
    public static UserDetailModel ToDetail(this User source)
        => new()
        {
            Id = source.Id,
            FirstName = source.FirstName,
            LastName = source.LastName,
        };
}

