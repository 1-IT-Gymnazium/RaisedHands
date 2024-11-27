using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Rooms
{
    public class RoomCreateModel
    {
        [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
        public string Name { get; set; } = null!;

        [Required]
        public Guid GroupId { get; set; }
    }
}
