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

    [HttpPatch("api/v1/Room/{id}")]
    public async Task<ActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] JsonPatchDocument<RoomCreateModel> patch
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

        var toUpdate = dbEntity.ToUpdate();

        patch.ApplyTo(toUpdate);

        var uniqueCheck = await _dbContext.Set<Room>().AnyAsync(x => x.Name == toUpdate.Name);

        if (uniqueCheck)
        {
            ModelState.AddModelError<RoomCreateModel>(x => x.Name, "name is not unique");
        }

        if (!(ModelState.IsValid && TryValidateModel(toUpdate)))
        {
            return ValidationProblem(ModelState);
        }

        dbEntity.Name = toUpdate.Name;

        await _dbContext.SaveChangesAsync();

        dbEntity = await _dbContext.Set<Room>().FirstAsync(x => x.Id == id);
        return Ok(dbEntity.ToDetail());
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

