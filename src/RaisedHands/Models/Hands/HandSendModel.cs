namespace RaisedHands.Api.Models.Hands;

public class HandSendModel
{
    public string RoomId { get; set; } = null!;

    public string GroupId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}
