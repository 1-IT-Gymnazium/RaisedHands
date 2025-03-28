using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Users;

public class RoomQuestionsAndHandsRaisedModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public List<QuestionModel> QuestionsAsked { get; set; } = null!;
    public int HandsRaisedCount { get; set; }
}

public static class RoomQuestionsAndHandsRaisedModelExtensions
{
    public static RoomQuestionsAndHandsRaisedModel ToStatsModel(this UserRoleGroup userGroup, List<Question> questions, int handsRaisedCount)
    {
        return new RoomQuestionsAndHandsRaisedModel
        {
            Id = userGroup.UserRole.User.Id,
            FirstName = userGroup.UserRole.User.FirstName,
            LastName = userGroup.UserRole.User.LastName,
            QuestionsAsked = questions.Select(q => q.ToQuestionModel()).ToList(),
            HandsRaisedCount = handsRaisedCount
        };
    }
}
