using RaisedHands.Api.Models.Rooms;
using RaisedHands.Api.Models.Users;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Groups;

public class GroupDetailModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public UserDetailModel Owner { get; set; } = null!;

    public List<RoomDetailModel>? Rooms;
}
public static class GroupDetailModelExtensions
{
    public static GroupDetailModel ToDetail(this Group source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            Code = source.Code,
            Owner = source.Owner.ToDetail(),
            Rooms = source.Rooms.Select(x => x.ToDetail()).ToList()
        };
}
