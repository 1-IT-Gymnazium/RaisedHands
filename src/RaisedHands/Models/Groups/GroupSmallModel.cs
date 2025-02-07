using RaisedHands.Api.Models.Rooms;
using RaisedHands.Api.Models.Users;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Groups;

public class GroupSmallModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public UserDetailModel Owner { get; set; } = null!;

}
public static class GroupSmallModelExtensions
{
    public static GroupSmallModel ToSmall(this Group source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            Owner = source.Owner.ToDetail(),
        };
}
