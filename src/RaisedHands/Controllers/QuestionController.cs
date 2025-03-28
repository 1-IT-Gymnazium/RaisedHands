using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using RaisedHands.Api.Hubs;
using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;
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

    /// <summary>
    /// Retrieves all questions for a specific room.
    /// </summary>
    /// <param name="roomId">The ID of the room.</param>
    /// <returns>
    /// 200 OK with a list of <see cref="QuestionReceiveModel"/> if any questions exist.<br/>
    /// 404 Not Found if the room or questions are not found.
    /// </returns>
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
            .ToListAsync();

        var result = dbEntities.Select(q => q.ToReceiveModel()).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Marks a question as answered by updating its AnsweredAt timestamp.
    /// </summary>
    /// <param name="questionId">The unique identifier of the question to update.</param>
    /// <returns>
    /// 200 OK with updated timestamp.<br/>
    /// 404 Not Found if the question is not found.
    /// </returns>
    [HttpPatch("api/v1/Question/{questionId}/Answered")]
    public async Task<ActionResult> UpdateAnsweredAt([FromRoute] Guid questionId)
    {
        var question = await _dbContext.Set<Question>().FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
        {
            return NotFound(new { Message = "Question not found." });
        }

        question.AnsweredAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("QuestionAnswered", questionId, question.AnsweredAt);

        return Ok(new { Message = "Question updated successfully.", AnsweredAt = question.AnsweredAt });
    }

    /// <summary>
    /// Permanently deletes a question from the database.
    /// </summary>
    /// <param name="questionId">The unique identifier of the question to delete.</param>
    /// <returns>
    /// 204 No Content if deletion is successful.<br/>
    /// 404 Not Found if the question is not found.
    /// </returns>
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
