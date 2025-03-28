using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Users;

public class UserQuestionsAndHandsRaisedModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public List<QuestionModel>? QuestionsAsked { get; set; }
    public List<HandRaisedModel>? HandsRaised { get; set; }
}

public static class UserModelExtensions
{
    public static UserQuestionsAndHandsRaisedModel ToUserQuestionsAndHandsRaisedModel(
        this UserRoleGroup userGroup,
        Guid userId,
        IEnumerable<Question> questions,
        IEnumerable<Hand> handsRaised)
    {
        return new UserQuestionsAndHandsRaisedModel
        {
            Id = userId,
            FirstName = userGroup.UserRole.User.FirstName,
            LastName = userGroup.UserRole.User.LastName,
            QuestionsAsked = questions.Any()
                ? questions.Select(q => q.ToQuestionModel()).ToList()
                : null,
            HandsRaised = handsRaised.Any()
                ? handsRaised.Select(hr => hr.ToRaisedModel()).ToList()
                : null
        };
    }
}
