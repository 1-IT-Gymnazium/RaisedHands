namespace RaisedHands.Api.Models.Questions;

public class QuestionHubModel
{
    public Guid RoomId { get; set; }

    public Guid From { get; set; }

    public string Message { get; set; } = null!;
}
