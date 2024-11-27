using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Models.Groups;

public class GroupDetailModel

{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public User Owner { get; set; } = null!;
}
public static class GroupDetailModelExtensions
{
    public static GroupDetailModel ToDetail(this Group source)
        => new()
        {
            Id = source.Id,
            Name = source.Name,
            Code = source.Code,
            Owner = source.Owner
        };
}
