using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace RaisedHands.Api.Models.Questions;

public class QuestionReceiveModel
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("roomId")]
    public string RoomId { get; set; } = null!;

    [JsonProperty("text")]
    public string Text { get; set; } = null!;

    //public string GroupId { get; set; } = null!;
    //public string? UserId { get; set; }
    [JsonProperty("userRoleGroupId")]
    public string? UserRoleGroupId { get; set; }

    [JsonProperty("sendAt")]
    public DateTime SendAt { get; set; }

    [JsonProperty("answeredAt")]
    public DateTime? AnsweredAt { get; set; }
}
