using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Rooms
{
    public class RoomDetailModel
    {
        public string Name { get; set; } = null!;

        public DateTime DateTime { get; set; }

        public DateTime? EndDate { get; set; }

        public Group GroupId { get; set; } = null!;
    }
}
