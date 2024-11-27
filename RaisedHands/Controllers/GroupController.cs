using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Utilities;
using RaisedHands.Data;
using RaisedHands.Data.Entities;
using RaisedHands.Data.Interfaces;

namespace RaisedHands.Api.Controllers;
[Authorize]
[ApiController]

public class GroupController : ControllerBase
{
    private readonly IClock _clock;
    private readonly AppDbContext _dbContext;

    public GroupController(
        IClock clock,
        AppDbContext dbContext
        )
    {
        _clock = clock;
        _dbContext = dbContext;
    }

    [HttpGet("api/v1/Group")]
    public async Task<ActionResult<IEnumerable<GroupDetailModel>>> GetList()
    {
        var dbEntities = await _dbContext
            .Set<Group>()
            .ToListAsync();

        return Ok(dbEntities);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <returns>The group detail</returns>
    [HttpGet("api/v1/Group/{id}")]
    public async Task<ActionResult<GroupDetailModel>> Get(
       [FromRoute] Guid id
       )
    {
        var dbEntity = await _dbContext
            .Set<Group>()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        var result = new GroupDetailModel
        {
            Name = dbEntity.Name,
            Code = dbEntity.Code,
        };

        return Ok(result);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/v1/Group")]
    public async Task<ActionResult> Create(
    [FromBody] GroupCreateModel model
    )
    {
        var now = _clock.GetCurrentInstant();

        var newGroup = new Group
        {
            Id = Guid.NewGuid(),
            Name = model.Name,
            OwnerId = User.GetUserId(),
            Code = "123" //vyřeším s ládou večer ;)"
        }.SetCreateBy(User.GetName(), now);

        var uniqueCheck = await _dbContext.Set<Group>().AnyAsync(x => x.Name == newGroup.Name);

        if (uniqueCheck)
        {
            ModelState.AddModelError<GroupCreateModel>(x => x.Name, "name is not unique");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        _dbContext.Add(newGroup);
        await _dbContext.SaveChangesAsync();

        var dbEntity = await _dbContext.Set<Group>().FirstAsync(x => x.Id == newGroup.Id);

        var url = Url.Action(nameof(Get), new { dbEntity.Id }) ?? throw new Exception("failed to generate url");
        return Created(url, dbEntity.ToDetail());

    }
    [HttpPatch("api/v1/Group/{id}")]

    public async Task<ActionResult<GroupDetailModel>> Update(
        [FromRoute] Guid id,
        [FromBody] JsonPatchDocument<GroupCreateModel> patch)
    {
        var dbEntity = await _dbContext.Set<Group>().FirstOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        var toUpdate = dbEntity.ToUpdate();

        patch.ApplyTo(toUpdate);

        var uniqueCheck = await _dbContext.Set<Group>().AnyAsync(x => x.Name == toUpdate.Name);

        if (uniqueCheck)
        {
            ModelState.AddModelError<GroupCreateModel>(x => x.Name, "name is not unique");
        }

        if (!(ModelState.IsValid && TryValidateModel(toUpdate)))
        {
            return ValidationProblem(ModelState);
        }

        dbEntity.Name = toUpdate.Name;

        await _dbContext.SaveChangesAsync();

        dbEntity = await _dbContext.Set<Group>().FirstAsync(x => x.Id == id);
        return Ok(dbEntity.ToDetail());

        /*[
  {
    "path": "/name",
    "op": "replace",
    "value": "name"
  }
]*/
    }

    [HttpDelete("api/v1/Group/{id}")]
    public async Task<ActionResult> Delete(
        [FromRoute] Guid id
    )
    {
        var dbEntity = await _dbContext
            .Set<Group>()
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
