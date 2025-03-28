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
   
    /// <summary>
    /// Retrieves all raised hands for a specific room.
    /// </summary>
    /// <param name="roomId">The ID of the room.</param>
    /// <returns>A list of raised hands if found, otherwise NotFound.</returns>
    [HttpGet("api/v1/Hand/{roomId}")]
    public async Task<ActionResult<List<HandReceiveModel>>> GetHandsByRoomId([FromRoute] Guid roomId)
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
                    Id = x.UserRoleGroup.UserRole.User.Id,
                    FirstName = x.UserRoleGroup.UserRole.User.FirstName,
                    LastName = x.UserRoleGroup.UserRole.User.LastName
                }
            })
            .ToListAsync();

        return Ok(dbEntities);
    }

    /// <summary>
    /// Marks a raised hand as answered by updating the AnsweredAt timestamp.
    /// </summary>
    /// <param name="handId">The ID of the raised hand.</param>
    /// <returns>HTTP 200 on success, NotFound if the hand does not exist.</returns>
    [HttpPatch("api/v1/Hand/{handId}/answered")]
    public async Task<ActionResult> UpdateAnsweredAt([FromRoute] Guid handId)
    {
        var hand = await _dbContext.Set<Hand>().FirstOrDefaultAsync(q => q.Id == handId);

        if (hand == null)
        {
            return NotFound(new { Message = "Hand not found." });
        }

        hand.AnsweredAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        Console.WriteLine($"📢 Sending HandLowered event for {handId}");
        await _hubContext.Clients.All.SendAsync("HandLowered", handId, hand.AnsweredAt);

        return Ok(new { Message = "Hand updated successfully.", AnsweredAt = hand.AnsweredAt });
    }
}
