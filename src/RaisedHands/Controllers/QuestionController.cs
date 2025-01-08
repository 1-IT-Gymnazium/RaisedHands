using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using RaisedHands.Api.Hubs;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Api.Models.Rooms;
using RaisedHands.Data;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Controllers;
[Authorize]
[ApiController]

public class QuestionController : ControllerBase
{
    private readonly IHubContext<QuestionHub> _hubContext;
    private readonly IClock _clock;
    private readonly DbContext _dbContext;

    public QuestionController(
        IHubContext<QuestionHub> hubContext,
        IClock clock,
        AppDbContext dbContext)
    {
        _hubContext = hubContext;
        _clock = clock;
        _dbContext = dbContext;
    }
    [HttpPost("api/v1/Question")]
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

        // Handle anonymous users
        if (model.IsAnonymous)
        {
            model.UserRoleGroupId = null;
        }

        // Create a new question
        var newQuestion = new Question
        {
            Id = Guid.NewGuid(),
            Text = model.Text,
            RoomId = model.RoomId,
            UserRoleGroupId = model.UserRoleGroupId,
            Answered = false,
            DateTime = DateTime.UtcNow
        };

        _dbContext.Add(newQuestion);
        await _dbContext.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("NewQuestionAdded", newQuestion);

        return Ok();
    }

    [HttpGet("api/v1/Question/{roomId}")]
    public async Task<ActionResult<QuestionDetailModel>> GetQuestionsByRoomId(
        [FromRoute] Guid roomId)
    {
        var dbEntities = await _dbContext
            .Set<Question>()
            .Where(x => x.RoomId == roomId)
            .OrderBy(q => q.DateTime)
            .ToListAsync();

        if (dbEntities == null || !dbEntities.Any())
        {
            return NotFound(new { Message = "No questions found for this room." });
        }

        return Ok(dbEntities);
    }
}
