namespace RaisedHands.Api.Models.Users;

public class UserDetailModel
{

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;
}

