using Newtonsoft.Json;
using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Users;
using RaisedHands.Data.Entities;
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

    [JsonProperty("user")]
    public QuestionUserDetailModel? User { get; set; } = null!;

}
public class QuestionUserDetailModel
{
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = null!;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = null!;
}
public static class QuestionExtensions
{
    public static QuestionReceiveModel ToReceiveModel(this Question question, UserDetailModel? userDetail = null)
    {
        QuestionUserDetailModel userModel;

        if (userDetail != null)
        {
            userModel = new QuestionUserDetailModel
            {
                Id = userDetail.Id,
                FirstName = userDetail.FirstName,
                LastName = userDetail.LastName
            };
        }
        else if (question.UserRoleGroup?.UserRole?.User != null)
        {
            var user = question.UserRoleGroup.UserRole.User;
            userModel = new QuestionUserDetailModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }
        else
        {
            userModel = new QuestionUserDetailModel
            {
                FirstName = "Anonymous",
                LastName = ""
            };
        }

        return new QuestionReceiveModel
        {
            Id = question.Id,
            RoomId = question.RoomId.ToString(),
            Text = question.Text,
            UserRoleGroupId = question.UserRoleGroupId?.ToString(),
            SendAt = question.SendAt,
            AnsweredAt = question.AnsweredAt,
            User = userModel
        };
    }
}

