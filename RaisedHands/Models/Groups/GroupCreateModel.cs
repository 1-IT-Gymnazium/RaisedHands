using RaisedHands.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace RaisedHands.Api.Models.Groups;

public class GroupCreateModel
{

    [Required(ErrorMessage = "{0} is required.", AllowEmptyStrings = false)]
    public string Name { get; set; } = null!;
}

public static class GroupCreateModelExtensions
{
    public static GroupCreateModel ToUpdate(this Group source)
        => new()
        {
            Name = source.Name
        };
}
