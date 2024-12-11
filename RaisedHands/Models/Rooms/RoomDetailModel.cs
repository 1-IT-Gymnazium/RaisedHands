using RaisedHands.Api.Models.Groups;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Rooms;

public class RoomDetailModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public DateTime DateTime { get; set; }

    public DateTime? EndDate { get; set; }

    public Group GroupId { get; set; } = null!;

}

public static class RoomDetailModelExtensions
{
    public static RoomDetailModel ToDetail(this Room source)
        => new()
        {
            Id = source.Id,
            Name = source.Name
        };
}
