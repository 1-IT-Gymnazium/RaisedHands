using RaisedHands.Api.Models.Users;
using RaisedHands.Data.Entities;
using System;

namespace RaisedHands.Api.Models.Questions
{
    public class QuestionDetailModel
      
    {
        public Guid Id { get; set; }
        public UserDetailModel? User { get; set; }

        public string RoomId { get; set; } = null!;

        public string Text { get; set; } = null!;

        public DateTime SendAt { get; set; }

        public DateTime? AnsweredAt { get; set; }

        public string GroupId { get; set; } = null!;
    }
}
