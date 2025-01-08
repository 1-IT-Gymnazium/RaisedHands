using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Questions
{
    public class QuestionDetailModel
    {
        public UserRoleGroup UserRoleGroup { get; set; } = null!;

        public Room Room { get; set; } = null!;

        public string Text { get; set; } = null!;

        public DateTime DateTime { get; set; }

        public bool Answered { get; set; }
    }
}
