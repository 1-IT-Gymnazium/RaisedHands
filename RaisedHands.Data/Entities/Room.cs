using NodaTime;
using RaisedHands.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities;

public class Room : ITrackable
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime DateTime { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public DateTime? EndDate { get; set; }
    public Instant CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public Instant ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = null!;
    public Instant? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
public static class RoomExtensions
{
    public static IQueryable<Room> FilterDeleted(this IQueryable<Room> query)
        => query
        .Where(x => x.DeletedAt == null)
        ;
}
