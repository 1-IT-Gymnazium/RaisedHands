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
        /// <summary>
        /// Allows a user to join a room if it is open.
        /// </summary>
        /// <param name="roomId">The unique identifier of the room.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task JoinRoom(string roomId)
        {
            var room = await _appDbContext.Set<Room>().FindAsync(Guid.Parse(roomId));

            if (room == null || room.EndDate != null)
            {
                await Clients.Caller.SendAsync("RoomClosed");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        /// <summary>
        /// Allows a user to leave a room.
        /// </summary>
        /// <param name="roomId">The unique identifier of the room.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        }

        /// <summary>
        /// Marks a question as answered and notifies all clients.
        /// </summary>
        /// <param name="questionId">The unique identifier of the question.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task MarkQuestionAsAnswered(string questionId)
        {
            await Clients.All.SendAsync("QuestionAnswered", questionId);
        }

        /// <summary>
        /// Deletes a question and notifies all clients.
        /// </summary>
        /// <param name="questionId">The unique identifier of the question.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DeleteQuestion(string questionId)
        {
            await Clients.All.SendAsync("QuestionDeleted", questionId);
        }

        /// <summary>
        /// Marks a raised hand as answered (lowered) and notifies all clients.
        /// </summary>
        /// <param name="handId">The unique identifier of the raised hand.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task MarkHandAsAnswered(string handId)
        {
            await Clients.All.SendAsync("HandLowered", handId);
        }

        /// <summary>
        /// Sends a message to a specific room.
        /// </summary>
        /// <param name="questionSendModel">The model containing message details including user ID, room ID, group ID, and message text.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendMessageToRoom(QuestionSendModel questionSendModel)
        {
            var userId = questionSendModel.UserId;
            var roomId = questionSendModel.RoomId;
            var message = questionSendModel.Text;
            var groupId = questionSendModel.GroupId;

            bool isAnonymous = string.IsNullOrEmpty(userId);

            if (isAnonymous)
            {
                userId = Guid.Empty.ToString();
            }

            var userGuid = new Guid(userId);

            Guid? userRoleGroupId = null;

            if (!isAnonymous)
            {
                var groupGuid = new Guid(groupId);

                var userRoles = _appDbContext.UserRoles
                    .Include(ur => ur.UserRoleGroups)
                    .Where(x => x.UserId == userGuid && x.UserRoleGroups.Any(y => y.GroupId == groupGuid));

                var userRole = userRoles.FirstOrDefault();

                if (userRole == null)
                {
                    throw new ArgumentException("No roles found for the user.");
                }

                var userRoleGroup = userRole.UserRoleGroups.FirstOrDefault(urg => urg.GroupId == groupGuid);

                if (userRoleGroup == null)
                {
                    throw new ArgumentException("No matching role group found for the user in the specified group.");
                }

                userRoleGroupId = userRoleGroup.Id;
            }

            var currentTime = DateTime.UtcNow;

            var newQuestion = new Question
            {
                Id = Guid.NewGuid(),
                Text = message,
                RoomId = Guid.Parse(roomId),
                SendAt = currentTime,
                UserRoleGroupId = userRoleGroupId,
                AnsweredAt = null
            };

            _appDbContext.Add(newQuestion);
            await _appDbContext.SaveChangesAsync();

            var user = isAnonymous ? null : await _appDbContext.Users
                .Where(u => u.Id == userGuid)
                .FirstOrDefaultAsync();

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
                    Id = userDetail?.Id ?? Guid.Empty,
                    FirstName = userDetail?.FirstName ?? "Anonymous",
                    LastName = userDetail?.LastName ?? ""
                }
            };

            await Clients.Group(roomId).SendAsync("ReceiveMessage", JsonConvert.SerializeObject(questionReceiveModel));
        }
        /// <summary>
        /// Sends a "hand raised" signal to a specific room.
        /// </summary>
        /// <param name="handSendModel">The model containing user ID, room ID, and group ID details.</param>
        /// <returns>A task representing the asynchronous operation, returning a HandReceiveModel containing the hand raise details.</returns>
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
                .Include(ur => ur.UserRoleGroups)
                .Where(x => x.UserId == userGuid && x.UserRoleGroups.Any(y => y.GroupId == groupGuid));

            var userRole = userRoles.FirstOrDefault();

            if (userRole == null)
            {
                throw new ArgumentException("No roles found for the user.");
            }

            var userRoleGroup = userRole.UserRoleGroups.FirstOrDefault(urg => urg.GroupId == groupGuid);

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

            var user = await _appDbContext.Users
                .Where(u => u.Id == userGuid)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            var userDetail = user.ToDetail();

            var handReceiveModel = new HandReceiveModel
            {
                Id = newHand.Id,
                RoomId = newHand.RoomId.ToString(),
                SendAt = newHand.SendAt,
                UserRoleGroupId = newHand.UserRoleGroupId.ToString(),
                User = new HandUserDetailModel
                {
                    Id = userDetail.Id,
                    FirstName = userDetail.FirstName,
                    LastName = userDetail.LastName
                }
            };

            await Clients.Group(roomId).SendAsync("ReceiveHand", JsonConvert.SerializeObject(handReceiveModel));

            return handReceiveModel;

        }
    }
}
