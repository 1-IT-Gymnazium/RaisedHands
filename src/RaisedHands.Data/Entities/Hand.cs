using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities
{
    public class Hand
    {
        public Guid Id { get; set; }

        public DateTime SendAt { get; set; }

        public DateTime AnsweredAt { get; set; }

        public Guid RoomId { get; set; }
        public Room Room { get; set; } = null!;

        public Guid UserRoleGroupId { get; set; }
        public UserRoleGroup UserRoleGroup { get; set; } = null!;
    }
}
