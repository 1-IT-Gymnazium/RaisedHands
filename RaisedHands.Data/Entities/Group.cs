using NodaTime;
using RaisedHands.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities
{
    public class Group : ITrackable
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string Code { get; set; } = null!;

        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;
        public Instant CreatedAt { get; set; }
        public string CreatedBy { get; set; } = null!;
        public Instant ModifiedAt { get; set; }
        public string ModifiedBy { get; set; } = null!;
        public Instant? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    public static class GroupExtensions
    {
        public static IQueryable<Group> FilterDeleted(this IQueryable<Group> query)
            => query
            .Where(x => x.DeletedAt == null)
            ;
    }
}
