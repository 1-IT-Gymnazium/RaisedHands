namespace RaisedHands.Api.Models.Auth;

public class LoggedUserModel
{
    public Guid id { get; set; }

    public string? email { get; set; }

    public string? name { get; set; } = string.Empty;

    public bool isAuthenticated { get; set; }
}
