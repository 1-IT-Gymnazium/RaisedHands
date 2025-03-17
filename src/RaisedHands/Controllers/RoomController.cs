using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using NodaTime.Text;
using NodaTime;
using RaisedHands.Data;
using RaisedHands.Api.Models.Rooms;
using Microsoft.EntityFrameworkCore;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Data.Entities;
using RaisedHands.Data.Interfaces;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using RaisedHands.Api.Hubs;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Api.Models.Users;

namespace RaisedHands.Api.Controllers;

[Authorize]
[ApiController]
public class RoomController : ControllerBase
{
    private readonly ILogger<RoomController> _logger;
    private readonly IClock _clock;
    private readonly AppDbContext _dbContext;

    public RoomController(
        ILogger<RoomController> logger,
        IClock clock,
        AppDbContext dbContext
        )
    {
        _clock = clock;
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retrieves a specific room by its ID.
    /// </summary>
    /// <param name="id">Room ID.</param>
    /// <returns>Room details if found, otherwise NotFound.</returns>
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
    /// Creates a new room.
    /// </summary>
    /// <param name="model">Room details.</param>
    /// <returns>HTTP 200 on success.</returns>
    [HttpPost("api/v1/Room")]
    public async Task<ActionResult> Create(
      [FromBody] RoomCreateModel model
      )
    {
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
    /// Ends an active room.
    /// </summary>
    /// <param name="id">Room ID.</param>
    /// <param name="patch">Patch document to modify the room.</param>
    /// <param name="hubContext">SignalR hub context for real-time updates.</param>
    /// <returns>Updated room details.</returns>
    [HttpPatch("api/v1/Room/{id}/end")]
    public async Task<ActionResult> EndRoom(
        [FromRoute] Guid id,
        [FromBody] JsonPatchDocument<Room> patch,
        [FromServices] IHubContext<QuestionHub> hubContext)
    {
        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == id);

        if (room == null)
        {
            return NotFound("Room not found.");
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
    /// Marks a room as deleted.
    /// </summary>
    /// <param name="id">Room ID.</param>
    /// <returns>HTTP 204 on success, NotFound if room is missing.</returns>
    [HttpDelete("api/v1/Room/{id}")]
    public async Task<ActionResult> Delete([FromRoute] Guid id)
    {
        var dbEntity = await _dbContext
            .Set<Room>()
            .FilterDeleted()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        dbEntity.SetDeleteBySystem(_clock.GetCurrentInstant());
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("api/v1/Room/{roomId}/UsersQuestionsAndHands")]
    public async Task<ActionResult<List<UserQuestionsAndHandsRaisedModel>>> GetUsersQuestionsAndHandsInRoom([FromRoute] Guid roomId)
    {
        var room = await _dbContext.Set<Room>()
            .Include(r => r.Group)
            .ThenInclude(g => g.UserGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.User)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null)
        {
            return NotFound(new { Message = "Room not found" });
        }

        var userStats = new List<RoomQuestionsAndHandsRaisedModel>();

        foreach (var userGroup in room.Group.UserGroups)
        {
            var userId = userGroup.UserRole.User.Id;

            // Fetch all questions for this user in the room
            var questions = await _dbContext.Set<Question>()
                .Where(q => q.RoomId == roomId && q.UserRoleGroup.UserRole.User.Id == userId)
                .Include(q => q.Room)
                .ToListAsync();

            // Count of hand raises for this user in the room
            var handsRaisedCount = await _dbContext.Set<Hand>()
                .Where(hr => hr.UserRoleGroup.UserRole.User.Id == userId && hr.RoomId == roomId)
                .CountAsync();

            userStats.Add(new RoomQuestionsAndHandsRaisedModel
            {
                UserId = userId,
                FirstName = userGroup.UserRole.User.FirstName,
                LastName = userGroup.UserRole.User.LastName,
                QuestionsAsked = questions.Select(q => new QuestionModel
                {
                    QuestionId = q.Id,
                    Content = q.Text,
                    RoomName = q.Room.Name
                }).ToList(),
                HandsRaisedCount = handsRaisedCount
            });
        }
        return Ok(userStats);
    }
}

