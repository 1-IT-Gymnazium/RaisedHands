using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Questions;

public class QuestionModel
{
    public Guid Id { get; set; }
    public string Text { get; set; } = null!;
    public DateTime DateAsked { get; set; }
    public string RoomName { get; set; } = null!;
}
public static class QuestionModelExtensions
{
    public static QuestionModel ToQuestionModel(this Question source)
        => new()
        {
            Id = source.Id,
            Text = source.Text,
            DateAsked = source.SendAt,
            RoomName = source.Room.Name
        };
}

