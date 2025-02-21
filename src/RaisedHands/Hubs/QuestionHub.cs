using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NodaTime;
using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Api.Models.Users;
using RaisedHands.Api.Utils;
using RaisedHands.Data;
using RaisedHands.Data.Entities;
using System.Reflection.Metadata;

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
            var room = await _appDbContext.Set<Room>().FindAsync(Guid.Parse(roomId));

            if (room == null || room.EndDate != null)
            {
                await Clients.Caller.SendAsync("RoomClosed"); // Notify user that room is closed
                return; // Prevent joining
            }

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

        public async Task DeleteQuestion(string questionId)
        {
            // Your logic to update the question's status, e.g., mark it as answered in the database
            // After that, notify all clients
            await Clients.All.SendAsync("QuestionDeleted", questionId);
        }

        public async Task MarkHandAsAnswered(string handId)
        {
            // Your logic to update the question's status, e.g., mark it as answered in the database
            // After that, notify all clients
            await Clients.All.SendAsync("HandLowered", handId);
        }

        public async Task SendMessageToRoom(QuestionSendModel questionSendModel)
        {
            var userId = questionSendModel.UserId;  // UserId may or may not be used for anonymous messages
            var roomId = questionSendModel.RoomId;
            var message = questionSendModel.Text;
            var groupId = questionSendModel.GroupId;

            // Check if the message is from an anonymous user (groupId is empty)
            bool isAnonymous = string.IsNullOrEmpty(userId);

            // If anonymous, set userId to a placeholder GUID
            if (isAnonymous)
            {
                userId = Guid.Empty.ToString();  // Use a placeholder for anonymous users
            }

            // Convert userId to GUID (use default GUID if anonymous)
            var userGuid = new Guid(userId);

            // Initialize userRoleGroupId as null for anonymous users
            Guid? userRoleGroupId = null;

            // If not anonymous, retrieve roles and group information
            if (!isAnonymous)
            {
                var groupGuid = new Guid(groupId);

                // Retrieve the user's roles using the GroupId and UserId
                var userRoles = _appDbContext.UserRoles
                    .Include(ur => ur.UserGroups)
                    .Where(x => x.UserId == userGuid && x.UserGroups.Any(y => y.GroupId == groupGuid));

                var userRole = userRoles.FirstOrDefault();

                if (userRole == null)
                {
                    throw new ArgumentException("No roles found for the user.");
                }

                var userRoleGroup = userRole.UserGroups.FirstOrDefault(urg => urg.GroupId == groupGuid);

                if (userRoleGroup == null)
                {
                    throw new ArgumentException("No matching role group found for the user in the specified group.");
                }

                userRoleGroupId = userRoleGroup.Id; // Use UserRoleGroupId for non-anonymous users
            }

            var currentTime = DateTime.UtcNow;

            // Create a new question object
            var newQuestion = new Question
            {
                Id = Guid.NewGuid(),
                Text = message,
                RoomId = Guid.Parse(roomId),  // roomId needs to be parsed as Guid
                SendAt = currentTime,
                UserRoleGroupId = userRoleGroupId,  // For anonymous users, this remains null
                AnsweredAt = null
            };

            _appDbContext.Add(newQuestion);
            await _appDbContext.SaveChangesAsync();

            // Retrieve user details for non-anonymous users
            var user = isAnonymous ? null : await _appDbContext.Users
                .Where(u => u.Id == userGuid)
                .FirstOrDefaultAsync();

            // If the user exists (non-anonymous), convert user to UserDetailModel
            var userDetail = user?.ToDetail();

            var questionReceiveModel = new QuestionReceiveModel
            {
                Id = newQuestion.Id,
                Text = newQuestion.Text,
                RoomId = newQuestion.RoomId.ToString(),
                SendAt = newQuestion.SendAt,
                UserRoleGroupId = newQuestion.UserRoleGroupId?.ToString(),
                AnsweredAt = newQuestion.AnsweredAt,
                User = new QuestionUserDetailModel
                {
                    FirstName = userDetail?.FirstName ?? "Anonymous",  // Use "Anonymous" for anonymous users
                    LastName = userDetail?.LastName ?? ""
                }
            };

            // Send the message to the SignalR group
            await Clients.Group(roomId).SendAsync("ReceiveMessage", JsonConvert.SerializeObject(questionReceiveModel));
        }

        //public async Task SendMessageToRoom(QuestionSendModel questionSendModel)
        //{
        //    // Extract the necessary information from the questionDetailModel
        //    var userId = questionSendModel.UserId;  // Assuming User is part of the QuestionDetailModel
        //    var roomId = questionSendModel.RoomId;
        //    var message = questionSendModel.Text;
        //    //var sendAt = questionSendModel.SendAt;
        //    var groupId = questionSendModel.GroupId;  // Assuming GroupId is part of the model

        //    // Convert userId and groupId to GUIDs
        //    var userGuid = new Guid(userId);
        //    var groupGuid = new Guid(groupId);

        //    // Retrieve the user's roles using the GroupId and UserId
        //    var userRoles = _appDbContext.UserRoles
        //        .Include(ur => ur.UserGroups) // Include UserGroups related to the UserRole
        //        .Where(x => x.UserId == userGuid && x.UserGroups.Any(y => y.GroupId == groupGuid));

        //    var userRole = userRoles
        //        .FirstOrDefault(); // Get the first match (assuming user can have multiple roles)

        //    if (userRole == null)
        //    {
        //        throw new ArgumentException("No roles found for the user.");
        //    }

        //    // Find the UserRoleGroupId related to the specified groupId
        //    var userRoleGroup = userRole.UserGroups
        //        .FirstOrDefault(urg => urg.GroupId == groupGuid);  // Assuming UserRoleGroup contains GroupId

        //    if (userRoleGroup == null)
        //    {
        //        throw new ArgumentException("No matching role group found for the user in the specified group.");
        //    }

        //    var userRoleGroupId = userRoleGroup.Id; // UserRoleGroupId for the UserRoleGroup

        //    var currentTime = DateTime.UtcNow;

        //    // Create a new question object using the model's details
        //    var newQuestion = new Question
        //    {
        //        Id = Guid.NewGuid(),
        //        Text = message,
        //        RoomId = Guid.Parse(roomId),  // roomId needs to be parsed as Guid
        //        SendAt = currentTime,
        //        UserRoleGroupId = userRoleGroupId,  // Set the UserRoleGroupId found above
        //        AnsweredAt = null
        //    };

        //    _appDbContext.Add(newQuestion);
        //    await _appDbContext.SaveChangesAsync();

        //    // Retrieve user details using the userId
        //    var user = await _appDbContext.Users
        //        .Where(u => u.Id == userGuid)
        //        .FirstOrDefaultAsync();

        //    if (user == null)
        //    {
        //        throw new ArgumentException("User not found.");
        //    }

        //    // Convert user to UserDetailModel using ToDetail extension method
        //    var userDetail = user.ToDetail();

        //    var questionReceiveModel = new QuestionReceiveModel
        //    {
        //        Id = newQuestion.Id,
        //        Text = newQuestion.Text,
        //        RoomId = newQuestion.RoomId.ToString(),
        //        SendAt = newQuestion.SendAt,
        //        UserRoleGroupId = newQuestion.UserRoleGroupId.ToString(),
        //        AnsweredAt = newQuestion.AnsweredAt,
        //        User = new QuestionUserDetailModel
        //        {
        //            FirstName = userDetail.FirstName,
        //            LastName = userDetail.LastName
        //        }
        //    };

        //    // Send the message to the SignalR group
        //    await Clients.Group(roomId).SendAsync("ReceiveMessage", JsonConvert.SerializeObject(questionReceiveModel));
        //}
        public async Task<HandReceiveModel> SendHandToRoom(HandSendModel handSendModel)
        {
            if (handSendModel.UserId == null)
            {
                throw new ArgumentException("User is required in the HandSendModel.");
            }

            var userId = handSendModel.UserId;
            var roomId = handSendModel.RoomId;
            var groupId = handSendModel.GroupId;

            var userGuid = new Guid(userId);
            var groupGuid = new Guid(groupId);

            var userRoles = _appDbContext.UserRoles
                .Include(ur => ur.UserGroups)
                .Where(x => x.UserId == userGuid && x.UserGroups.Any(y => y.GroupId == groupGuid));

            var userRole = userRoles.FirstOrDefault();

            if (userRole == null)
            {
                throw new ArgumentException("No roles found for the user.");
            }

            var userRoleGroup = userRole.UserGroups.FirstOrDefault(urg => urg.GroupId == groupGuid);

            if (userRoleGroup == null)
            {
                throw new ArgumentException("No matching role group found for the user in the specified group.");
            }

            var userRoleGroupId = userRoleGroup.Id;
            var currentTime = DateTime.UtcNow;

            var newHand = new Hand
            {
                Id = Guid.NewGuid(),
                RoomId = Guid.Parse(roomId),
                SendAt = currentTime,
                UserRoleGroupId = userRoleGroupId
            };

            _appDbContext.Add(newHand);
            await _appDbContext.SaveChangesAsync();

            // Retrieve user details using the userId
            var user = await _appDbContext.Users
                .Where(u => u.Id == userGuid)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            // Convert user to UserDetailModel using ToDetail extension method
            var userDetail = user.ToDetail();

            // Create the HandReceiveModel and include user details
            var handReceiveModel = new HandReceiveModel
            {
                Id = newHand.Id,
                RoomId = newHand.RoomId.ToString(),
                SendAt = newHand.SendAt,
                UserRoleGroupId = newHand.UserRoleGroupId.ToString(),
                // Include the user's first name and last name
                User = new HandUserDetailModel
                {
                    FirstName = userDetail.FirstName,
                    LastName = userDetail.LastName
                }
            };

            // Send the hand raise information to the group
            await Clients.Group(roomId).SendAsync("ReceiveHand", JsonConvert.SerializeObject(handReceiveModel));

            return handReceiveModel;

        }
    }
}
