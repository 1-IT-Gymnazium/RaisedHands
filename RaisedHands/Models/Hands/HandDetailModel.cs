using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Hands
{
    public class HandDetailModel
    {
        public User User { get; set; } = null!;

        public DateTime DateTime { get; set; }

        public bool Answered { get; set; }
    }
}
