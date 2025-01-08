using RaisedHands.Api.Models.Groups;
using RaisedHands.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Rooms;

public class RoomCreateModel
{
    [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
    public Guid GroupId { get; set; }
}
public static class RoomCreateModelExtensions
{
public static RoomCreateModel ToUpdate(this Room source)
    => new()
    {
        Name = source.Name
    };
}
