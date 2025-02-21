namespace RaisedHands.Api.Models.Questions;

public class QuestionModel
{
    public Guid QuestionId { get; set; }
    public string Content { get; set; }
    public DateTime DateAsked { get; set; }
    public string RoomName { get; set; }  // Room Name where the question was asked
}
