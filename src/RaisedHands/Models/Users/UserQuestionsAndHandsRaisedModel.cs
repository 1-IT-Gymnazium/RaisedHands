using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;

namespace RaisedHands.Api.Models.Users;

public class UserQuestionsAndHandsRaisedModel
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public List<QuestionModel> QuestionsAsked { get; set; }
    public List<HandRaisedModel> HandsRaised { get; set; }
}
