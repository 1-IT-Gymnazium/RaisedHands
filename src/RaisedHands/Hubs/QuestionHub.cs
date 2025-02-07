using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NodaTime;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Api.Utils;
using RaisedHands.Data;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Hubs
{
    public class QuestionHub : Hub
    {
        private readonly IClock _clock;
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _appDbContext;
        public QuestionHub(IClock clock,
        UserManager<User> userManager, AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
            _clock = clock;
            _userManager = userManager;
        }
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        }

        // Method called by client to mark a question as answered
        public async Task MarkQuestionAsAnswered(string questionId)
        {
            // Your logic to update the question's status, e.g., mark it as answered in the database
            // After that, notify all clients
            await Clients.All.SendAsync("QuestionAnswered", questionId);
        }

        public async Task SendMessageToRoom(QuestionSendModel questionSendModel)
        {
            Context.User.GetUserId();
            // Ensure the User property is not null
            if (questionSendModel?.UserId == null)
            {
                throw new ArgumentException("User is required in the QuestionDetailModel.");
            }

            // Extract the necessary information from the questionDetailModel
            var userId = questionSendModel.UserId;  // Assuming User is part of the QuestionDetailModel
            var roomId = questionSendModel.RoomId;
            var message = questionSendModel.Text;
            //var sendAt = questionSendModel.SendAt;
            var groupId = questionSendModel.GroupId;  // Assuming GroupId is part of the model

            // Convert userId and groupId to GUIDs
            var userGuid = new Guid(userId);
            var groupGuid = new Guid(groupId);

            // Retrieve the user's roles using the GroupId and UserId
            var userRole = await _appDbContext.UserRoles
                .Include(ur => ur.UserGroups) // Include UserGroups related to the UserRole
                .Where(x => x.UserId == userGuid)
                .FirstOrDefaultAsync(); // Get the first match (assuming user can have multiple roles)

            if (userRole == null)
            {
                throw new ArgumentException("No roles found for the user.");
            }

            // Find the UserRoleGroupId related to the specified groupId
            var userRoleGroup = userRole.UserGroups
                .FirstOrDefault(urg => urg.GroupId == groupGuid);  // Assuming UserRoleGroup contains GroupId

            if (userRoleGroup == null)
            {
                throw new ArgumentException("No matching role group found for the user in the specified group.");
            }

            var userRoleGroupId = userRoleGroup.Id; // UserRoleGroupId for the UserRoleGroup

            var currentTime = DateTime.UtcNow;

            // Create a new question object using the model's details
            var newQuestion = new Question
            {
                Id = Guid.NewGuid(),
                Text = message,
                RoomId = Guid.Parse(roomId),  // roomId needs to be parsed as Guid
                SendAt = currentTime,
                UserRoleGroupId = userRoleGroupId,  // Set the UserRoleGroupId found above
                AnsweredAt = null
            };

            _appDbContext.Add(newQuestion);
            await _appDbContext.SaveChangesAsync();

            var questionReceiveModel = new QuestionReceiveModel
            {
                Id = newQuestion.Id,
                Text = newQuestion.Text,
                RoomId = newQuestion.RoomId.ToString(),
                SendAt = newQuestion.SendAt,
                UserRoleGroupId = newQuestion.UserRoleGroupId.ToString(),
                AnsweredAt = newQuestion.AnsweredAt
            };

            // Send the message to the SignalR group
            await Clients.Group(roomId).SendAsync("ReceiveMessage", JsonConvert.SerializeObject(questionReceiveModel));
        }
    }
}
