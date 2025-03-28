namespace RaisedHands.Api.Models.Auth;

public class LoggedUserModel
{
    public Guid Id { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; } = string.Empty;

    public bool IsAuthenticated { get; set; }
}
