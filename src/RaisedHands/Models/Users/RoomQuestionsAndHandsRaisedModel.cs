using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;

namespace RaisedHands.Api.Models.Users;

public class RoomQuestionsAndHandsRaisedModel
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public List<QuestionModel> QuestionsAsked { get; set; } = null!;
    public int HandsRaisedCount { get; set; }
}
