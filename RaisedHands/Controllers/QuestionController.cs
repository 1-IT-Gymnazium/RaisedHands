using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using RaisedHands.Api.Models.Questions;
using RaisedHands.Data;
using RaisedHands.Data.Entities;

namespace RaisedHands.Api.Controllers;
[Authorize]
[ApiController]

public class QuestionController : ControllerBase
{
    private readonly IClock _clock;
    private readonly DbContext _dbContext;

    public QuestionController(
        IClock clock,
        AppDbContext dbContext)
    {
        _clock = clock;
        _dbContext = dbContext;
    }
    [HttpPost("api/v1/Question")]
    public async Task<ActionResult> Create(
    [FromBody] QuestionCreateModel model
)
    {
        var now = _clock.GetCurrentInstant();

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

        if (model.IsAnonymous)
        {
            model.UserRoleGroupId = null;
        }

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

        return Ok();
    }
}
