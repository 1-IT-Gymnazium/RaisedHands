using Newtonsoft.Json;

namespace RaisedHands.Api.Models;

public class IdNameModel
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = null!;
}
