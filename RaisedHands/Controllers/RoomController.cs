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

    [HttpGet("api/v1/Room")]
    public async Task<ActionResult<IEnumerable<RoomDetailModel>>> GetList()
    {
        var dbEntities = await _dbContext
            .Set<Room>()
            .ToListAsync();

        return Ok(dbEntities);
    }

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

        var result = new RoomDetailModel
        {
            Name = dbEntity.Name
        };

        return Ok(result);
    }

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

    [HttpDelete("api/v1/Room/{id}")]
    public async Task<ActionResult> Delete(
    [FromRoute] Guid id
)
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
}

