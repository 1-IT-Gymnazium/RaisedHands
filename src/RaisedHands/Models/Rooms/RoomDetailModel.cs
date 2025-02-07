using RaisedHands.Api.Models.Groups;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Rooms;

public class RoomDetailModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public DateTime DateTime { get; set; }

    public DateTime? EndDate { get; set; }

    public Guid GroupId { get; set; } 
}

public static class RoomDetailModelExtensions
{
    public static RoomDetailModel ToDetail(this Room source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            DateTime = source.DateTime,
            EndDate = source.EndDate,
            GroupId = source.GroupId
        };
}
