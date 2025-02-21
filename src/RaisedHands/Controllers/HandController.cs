using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using RaisedHands.Api.Hubs;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Api.Models.Rooms;
using RaisedHands.Data;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Controllers;
[Authorize]
[ApiController]

public class HandController : ControllerBase
{
    private readonly IHubContext<QuestionHub> _hubContext;
    private readonly IClock _clock;
    private readonly DbContext _dbContext;

    public HandController(
        IHubContext<QuestionHub> hubContext,
        IClock clock,
        AppDbContext dbContext)
    {
        _hubContext = hubContext;
        _clock = clock;
        _dbContext = dbContext;
    }
    [HttpPost("api/v1/Hand")]
    public async Task<ActionResult> Create(
    [FromBody] QuestionCreateModel model
)
    {
        var now = _clock.GetCurrentInstant();

        // Try to get RoomId and UserRoleGroupId from cookies
        var roomIdFromCookie = HttpContext.Request.Cookies["RoomId"];
        var userRoleGroupIdFromCookie = HttpContext.Request.Cookies["UserRoleGroupId"];

        // If not found in cookies, fallback to the request model
        if (string.IsNullOrEmpty(roomIdFromCookie) || string.IsNullOrEmpty(userRoleGroupIdFromCookie))
        {
            return BadRequest(new { Message = "RoomId or UserRoleGroupId not found in cookies or request" });
        }

        model.RoomId = Guid.Parse(roomIdFromCookie);  // Assuming cookies store the GUID as a string
        model.UserRoleGroupId = Guid.Parse(userRoleGroupIdFromCookie);  // Assuming cookies store the GUID as a string

        // Check if room exists
        var roomExists = await _dbContext.Set<Room>().AnyAsync(r => r.Id == model.RoomId);
        if (!roomExists)
        {
            return NotFound(new { Message = "Specified room does not exist" });
        }

        // Check if user role group exists
        var userGroupExists = await _dbContext.Set<UserRoleGroup>().AnyAsync(ug => ug.Id == model.UserRoleGroupId);
        if (!userGroupExists)
        {
            return NotFound(new { Message = "Specified user group does not exist" });
        }

        // Create a new question
        var newQuestion = new Question
        {
            Id = Guid.NewGuid(),
            Text = model.Text,
            RoomId = model.RoomId,
            UserRoleGroupId = model.UserRoleGroupId,
            AnsweredAt = null,
            SendAt = DateTime.UtcNow
        };

        _dbContext.Add(newQuestion);
        await _dbContext.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("NewHandAdded", newQuestion);

        return Ok();
    }

    [HttpGet("api/v1/Hand/{roomId}")]
    public async Task<ActionResult<List<HandReceiveModel>>> GetHandsByRoomId(
    [FromRoute] Guid roomId)
    {
        var dbEntities = await _dbContext
            .Set<Hand>()
            .Where(x => x.RoomId == roomId)
            .OrderBy(q => q.SendAt)
            .Select(x => new HandReceiveModel
            {
                Id = x.Id,
                RoomId = x.RoomId.ToString(),
                UserRoleGroupId = x.UserRoleGroupId.ToString(),
                SendAt = x.SendAt,
                AnsweredAt = x.AnsweredAt,
                User = new HandUserDetailModel
                {
                    // Assuming UserRoleGroup is linked to UserRole, and UserRole has a User
                    Id = x.UserRoleGroup.UserRole.User.Id, // Accessing User via UserRole
                    FirstName = x.UserRoleGroup.UserRole.User.FirstName, // Accessing User's FirstName
                    LastName = x.UserRoleGroup.UserRole.User.LastName // Accessing User's LastName
                }
            })
            .ToListAsync();

        if (dbEntities == null || !dbEntities.Any())
        {
            return NotFound(new { Message = "No hands found for this room." });
        }

        return Ok(dbEntities);
    }

    [HttpPatch("api/v1/Hand/{handId}/answered")]
    public async Task<ActionResult> UpdateAnsweredAt(
    [FromRoute] Guid handId)
    {
        // Find the question in the database
        var hand = await _dbContext.Set<Hand>().FirstOrDefaultAsync(q => q.Id == handId);

        if (hand == null)
        {
            return NotFound(new { Message = "Hand not found." });
        }

        // Update the AnsweredAt timestamp to the current time
        hand.AnsweredAt = DateTime.UtcNow;

        // Save changes to the database
        await _dbContext.SaveChangesAsync();

        // Notify clients via SignalR
        Console.WriteLine($"📢 Sending HandLowered event for {handId}");
        await _hubContext.Clients.All.SendAsync("HandLowered", handId, hand.AnsweredAt);

        return Ok(new { Message = "Hand updated successfully.", AnsweredAt = hand.AnsweredAt });
    }
}
