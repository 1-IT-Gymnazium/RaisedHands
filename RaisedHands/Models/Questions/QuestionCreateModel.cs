using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Questions
{
    public class QuestionCreateModel
    {
        [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
        public string? text { get; set; }
    }
}
