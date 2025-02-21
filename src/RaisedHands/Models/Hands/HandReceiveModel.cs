using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace RaisedHands.Api.Models.Hands
{
    public class HandReceiveModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("roomId")]
        public string RoomId { get; set; } = null!;

        [JsonProperty("userRoleGroupId")]
        public string UserRoleGroupId { get; set; } = null!;

        [JsonProperty("sendAt")]
        public DateTime SendAt { get; set; }

        [JsonProperty("answeredAt")]
        public DateTime? AnsweredAt { get; set; }

        // Add properties for user's first and last name
        [JsonProperty("user")]
        public HandUserDetailModel User { get; set; } = null!;
    }

    public class HandUserDetailModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; } = null!;

        [JsonProperty("lastName")]
        public string LastName { get; set; } = null!;
    }
}
