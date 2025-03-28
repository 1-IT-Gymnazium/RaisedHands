using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Hands;

public class HandRaisedModel
{
    public Guid Id { get; set; }
    public string RoomName { get; set; } = null!;
}

public static class HandRaisedModelExtensions
{
    public static HandRaisedModel ToRaisedModel(this Hand source)
        => new()
        {
            Id = source.Id,
            RoomName = source.Room.Name
        };
}
