using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using NodaTime;
using RaisedHands.Data;
using RaisedHands.Api.Models.Rooms;
using Microsoft.EntityFrameworkCore;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Data.Entities;
using RaisedHands.Data.Interfaces;
using Microsoft.AspNetCore.SignalR;
using RaisedHands.Api.Hubs;
using RaisedHands.Api.Models.Users;
using System.Security.Claims;
using RaisedHands.Api.Utils;
using RaisedHands.Api.Services;

namespace RaisedHands.Api.Controllers;

[Authorize]
[ApiController]
public class RoomController : ControllerBase
{
    private readonly IClock _clock;
    private readonly AppDbContext _dbContext;
    private readonly UserService _userService;

    public RoomController(
        IClock clock,
        AppDbContext dbContext,
        UserService userService
        )
    {
        _clock = clock;
        _dbContext = dbContext;
        _userService = userService;
    }

    /// <summary>
    /// Retrieves a specific room by its unique identifier.
    /// </summary>
    /// <param name="id">The ID of the room to retrieve.</param>
    /// <returns>
    /// 200 OK with <see cref="RoomDetailModel"/> if found.<br/>
    /// 404 Not Found if the room does not exist.
    /// </returns>
    [HttpGet("api/v1/Room/{id}")]
    public async Task<ActionResult<RoomDetailModel>> Get(
   [FromRoute] Guid id
   )
    {
        var dbEntity = await _dbContext
            .Set<Room>()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        var result = dbEntity.ToDetail();

        return Ok(result);
    }

    /// <summary>
    /// Creates a new room within a group. Only users with the Teacher role can create rooms.
    /// </summary>
    /// <param name="model">The model containing room creation data.</param>
    /// <returns>
    /// 200 OK if creation is successful.<br/>
    /// 401 Unauthorized if user is not authenticated.<br/>
    /// 403 Forbidden if the user is not a teacher in the group.
    /// </returns>
    [HttpPost("api/v1/Room")]
    public async Task<ActionResult> Create([FromBody] RoomCreateModel model)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        bool isTeacher = await _userService.IsUserTeacherInGroup(userId, model.GroupId);

        if (!isTeacher)
        {
            return Forbid("Only teachers in this group can create rooms.");
        }

        var now = _clock.GetCurrentInstant();
        var newRoom = new Room
        {
            Id = Guid.NewGuid(),
            Name = model.Name,
            GroupId = model.GroupId,
        }.SetCreateBySystem(now);

        _dbContext.Add(newRoom);
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    /// Ends an active room session. Notifies clients via SignalR that the room is closed.
    /// </summary>
    /// <param name="id">The ID of the room to end.</param>
    /// <param name="patch">Patch document containing updates to the room.</param>
    /// <param name="hubContext">SignalR hub context for broadcasting to connected clients.</param>
    /// <returns>
    /// 200 OK with the updated room.<br/>
    /// 401 Unauthorized if the user is not authenticated.<br/>
    /// 403 Forbidden if the user is not authorized.<br/>
    /// 404 Not Found if the room does not exist.
    /// </returns>
    [HttpPatch("api/v1/Room/{id}/End")]
    public async Task<ActionResult> EndRoom(
    [FromRoute] Guid id,
    [FromBody] JsonPatchDocument<Room> patch,
    [FromServices] IHubContext<QuestionHub> hubContext)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room == null)
        {
            return NotFound("Room not found.");
        }

        bool isTeacher = await _userService.IsUserTeacherInGroup(userId, room.GroupId);
        if (!isTeacher)
        {
            return Forbid("Only teachers in this group can end a room.");
        }

        patch.ApplyTo(room);

        if (room.EndDate == null)
        {
            room.EndDate = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        await hubContext.Clients.Group(id.ToString()).SendAsync("RoomClosed");

        return Ok(room);
    }

    /// <summary>
    /// Soft-deletes a room. Only teachers in the group can perform this action.
    /// </summary>
    /// <param name="id">The ID of the room to delete.</param>
    /// <returns>
    /// 204 No Content on success.<br/>
    /// 401 Unauthorized if user is not authenticated.<br/>
    /// 403 Forbidden if user is not a teacher in the group.<br/>
    /// 404 Not Found if the room does not exist.
    /// </returns>
    [HttpDelete("api/v1/Room/{id}")]
    public async Task<ActionResult> Delete([FromRoute] Guid id)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        var dbEntity = await _dbContext
            .Set<Room>()
            .FilterDeleted()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        bool isTeacher = await _userService.IsUserTeacherInGroup(userId, dbEntity.GroupId);
        if (!isTeacher)
        {
            return Forbid("Only teachers in this group can delete a room.");
        }

        dbEntity.SetDeleteBySystem(_clock.GetCurrentInstant());
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Retrieves a list of users in the room with their questions and hand raise statistics.
    /// </summary>
    /// <param name="roomId">The ID of the room to analyze.</param>
    /// <returns>
    /// 200 OK with a list of <see cref="RoomQuestionsAndHandsRaisedModel"/>.<br/>
    /// 404 Not Found if the room is not found.
    /// </returns>
    [HttpGet("api/v1/Room/{roomId}/UsersQuestionsAndHands")]
    public async Task<ActionResult<List<UserQuestionsAndHandsRaisedModel>>> GetUsersQuestionsAndHandsInRoom([FromRoute] Guid roomId)
    {
        var room = await _dbContext.Set<Room>()
            .Include(r => r.Group)
            .ThenInclude(g => g.UserRoleGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.User)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null)
        {
            return NotFound(new { Message = "Room not found" });
        }

        var userStats = new List<RoomQuestionsAndHandsRaisedModel>();

        foreach (var userGroup in room.Group.UserRoleGroups)
        {
            var userId = userGroup.UserRole.User.Id;

            var questions = await _dbContext.Set<Question>()
                .Where(q => q.RoomId == roomId && q.UserRoleGroup.UserRole.User.Id == userId)
                .Include(q => q.Room)
                .ToListAsync();

            var handsRaisedCount = await _dbContext.Set<Hand>()
                .Where(hr => hr.UserRoleGroup.UserRole.User.Id == userId && hr.RoomId == roomId)
                .CountAsync();

            userStats.Add(userGroup.ToStatsModel(questions, handsRaisedCount));
        }
        return Ok(userStats);
    }
}

