using Newtonsoft.Json;
using RaisedHands.Api.Models.Users;
using RaisedHands.Data.Entities;
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
    public static class HandReceiveModelExtensions
    {
        public static HandReceiveModel ToReceiveModel(this Hand hand)
        {
            return new HandReceiveModel
            {
                Id = hand.Id,
                RoomId = hand.RoomId.ToString(),
                UserRoleGroupId = hand.UserRoleGroupId.ToString(),
                SendAt = hand.SendAt,
                AnsweredAt = hand.AnsweredAt,
                User = new HandUserDetailModel
                {
                    Id = hand.UserRoleGroup.UserRole.User.Id,
                    FirstName = hand.UserRoleGroup.UserRole.User.FirstName,
                    LastName = hand.UserRoleGroup.UserRole.User.LastName
                }
            };
        }
    }
}

