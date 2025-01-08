using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Questions
{
    public class QuestionCreateModel
    {
        [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
        public string Text { get; set; } = null!;

        [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
        public Guid RoomId { get; set; }

        public Guid? UserRoleGroupId { get; set; }

        public bool IsAnonymous { get; set; }
    }
}
