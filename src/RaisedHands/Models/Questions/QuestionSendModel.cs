using RaisedHands.Api.Models.Users;

namespace RaisedHands.Api.Models.Questions;

public class QuestionSendModel
{

    public string RoomId { get; set; } = null!;

    public string Text { get; set; } = null!;

    public string GroupId { get; set; } = null!;
    public string?  UserId { get; set; }
}
