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
using System.Reflection.Metadata;

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

        await _hubContext.Clients.All.SendAsync("NewQuestionAdded", newQuestion);

        return Ok();
    }

    [HttpGet("api/v1/Question/{roomId}")]
    public async Task<ActionResult<QuestionReceiveModel>> GetQuestionsByRoomId(
        [FromRoute] Guid roomId)
    {
        var dbEntities = await _dbContext
      .Set<Question>()
      .Include(q => q.UserRoleGroup).ThenInclude(u => u.UserRole).ThenInclude(r => r.User)
      .Where(x => x.RoomId == roomId)
      .OrderBy(q => q.SendAt)
      .Select(x => new QuestionReceiveModel
      {
          Id = x.Id,
          RoomId = x.RoomId.ToString(),
          Text = x.Text,
          UserRoleGroupId = x.UserRoleGroupId.ToString(),
          SendAt = x.SendAt,
          AnsweredAt = x.AnsweredAt,
          User = x.UserRoleGroup.UserRole.User != null ? new QuestionUserDetailModel
          {
              Id = x.UserRoleGroup.UserRole.User.Id, // Accessing User via UserRole
              FirstName = x.UserRoleGroup.UserRole.User.FirstName, // Accessing User's FirstName
              LastName = x.UserRoleGroup.UserRole.User.LastName // Accessing User's LastName
          } : new QuestionUserDetailModel
          {
              FirstName = "User", // Default name for anonymous user
              LastName = "Anonym"  // Default last name for anonymous user
          }
      })
      .ToListAsync();

        if (dbEntities == null || !dbEntities.Any())
        {
            return NotFound(new { Message = "No questions found for this room." });
        }

        return Ok(dbEntities);
    }

    [HttpPatch("api/v1/Question/{questionId}/answered")]
    public async Task<ActionResult> UpdateAnsweredAt(
    [FromRoute] Guid questionId)
    {
        // Find the question in the database
        var question = await _dbContext.Set<Question>().FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
        {
            return NotFound(new { Message = "Question not found." });
        }

        // Update the AnsweredAt timestamp to the current time
        question.AnsweredAt = DateTime.UtcNow;

        // Save changes to the database
        await _dbContext.SaveChangesAsync();

        // Notify clients via SignalR
        Console.WriteLine($"📢 Sending HandLowered event for {questionId}");
        await _hubContext.Clients.All.SendAsync("QuestionAnswered", questionId, question.AnsweredAt);

        return Ok(new { Message = "Question updated successfully.", AnsweredAt = question.AnsweredAt });
    }

    [HttpDelete("api/v1/Question/{questionId}")]
    public async Task<IActionResult> DeleteQuestion(
    [FromRoute] Guid questionId)
    {
        var question = await _dbContext.Set<Question>().FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
        {
            return NotFound(new { Message = "Question not found." });
        }

        _dbContext.Remove(question);
        await _dbContext.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("QuestionDeleted", questionId);

        return NoContent();
    }
}
