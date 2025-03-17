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
    /// <summary>
    /// Creates a new question using RoomId and UserRoleGroupId from cookies.
    /// </summary>
    /// <param name="model">The question creation model containing text.</param>
    /// <returns>HTTP 200 if created successfully, BadRequest or NotFound otherwise.</returns>
    [HttpPost("api/v1/Question")]
    public async Task<ActionResult> Create([FromBody] QuestionCreateModel model)
    {
        var now = _clock.GetCurrentInstant();

        var roomIdFromCookie = HttpContext.Request.Cookies["RoomId"];
        var userRoleGroupIdFromCookie = HttpContext.Request.Cookies["UserRoleGroupId"];

        if (string.IsNullOrEmpty(roomIdFromCookie) || string.IsNullOrEmpty(userRoleGroupIdFromCookie))
        {
            return BadRequest(new { Message = "RoomId or UserRoleGroupId not found in cookies or request" });
        }

        model.RoomId = Guid.Parse(roomIdFromCookie);
        model.UserRoleGroupId = Guid.Parse(userRoleGroupIdFromCookie);

        var roomExists = await _dbContext.Set<Room>().AnyAsync(r => r.Id == model.RoomId);
        if (!roomExists)
        {
            return NotFound(new { Message = "Specified room does not exist" });
        }

        var userGroupExists = await _dbContext.Set<UserRoleGroup>().AnyAsync(ug => ug.Id == model.UserRoleGroupId);
        if (!userGroupExists)
        {
            return NotFound(new { Message = "Specified user group does not exist" });
        }

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

    /// <summary>
    /// Retrieves all questions for a specific room.
    /// </summary>
    /// <param name="roomId">The ID of the room.</param>
    /// <returns>A list of questions if found, otherwise NotFound.</returns>
    [HttpGet("api/v1/Question/{roomId}")]
    public async Task<ActionResult<QuestionReceiveModel>> GetQuestionsByRoomId([FromRoute] Guid roomId)
    {
        var dbEntities = await _dbContext
            .Set<Question>()
            .Include(q => q.UserRoleGroup)
            .ThenInclude(u => u.UserRole)
            .ThenInclude(r => r.User)
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
                    Id = x.UserRoleGroup.UserRole.User.Id,
                    FirstName = x.UserRoleGroup.UserRole.User.FirstName,
                    LastName = x.UserRoleGroup.UserRole.User.LastName
                } : new QuestionUserDetailModel
                {
                    FirstName = "User",
                    LastName = "Anonym"
                }
            })
            .ToListAsync();

        return Ok(dbEntities);
    }

    /// <summary>
    /// Marks a question as answered by updating the AnsweredAt timestamp.
    /// </summary>
    /// <param name="questionId">The ID of the question.</param>
    /// <returns>HTTP 200 on success, NotFound if the question does not exist.</returns>
    [HttpPatch("api/v1/Question/{questionId}/answered")]
    public async Task<ActionResult> UpdateAnsweredAt([FromRoute] Guid questionId)
    {
        var question = await _dbContext.Set<Question>().FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
        {
            return NotFound(new { Message = "Question not found." });
        }

        question.AnsweredAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        Console.WriteLine($"📢 Sending HandLowered event for {questionId}");
        await _hubContext.Clients.All.SendAsync("QuestionAnswered", questionId, question.AnsweredAt);

        return Ok(new { Message = "Question updated successfully.", AnsweredAt = question.AnsweredAt });
    }

    /// <summary>
    /// Deletes a question from the database.
    /// </summary>
    /// <param name="questionId">The ID of the question.</param>
    /// <returns>HTTP 204 on success, NotFound if the question does not exist.</returns>
    [HttpDelete("api/v1/Question/{questionId}")]
    public async Task<IActionResult> DeleteQuestion([FromRoute] Guid questionId)
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
