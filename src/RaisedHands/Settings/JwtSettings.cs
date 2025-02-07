namespace RaisedHands.Api.Settings;

public class JwtSettings
{
    public required string SecretKey { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required int AccessTokenExpirationInMinutes { get; set; }
    public required int RefreshTokenExpirationInDays { get; set; }
}
