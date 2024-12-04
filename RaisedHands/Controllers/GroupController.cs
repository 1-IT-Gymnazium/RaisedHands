using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Models.Rooms;
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
            .Include(x => x.Rooms)
            .Include(x => x.Owner)
            .ToListAsync();

        return Ok(dbEntities.Select(x => x.ToDetail()));
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
            .Include(x => x.Rooms)
            .Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        var result = dbEntity.ToDetail();

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
            Code = await GenerateUniqueCodeAsync(8)
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

        // Check if the user already has the Teacher role in UserRole
        var userId = User.GetUserId();
        var teacherRoleId = (await _dbContext.Set<Role>().FirstAsync(x => x.Name == "Teacher")).Id; // Retrieve Teacher Role Id

        // Fetch UserRole for this user and role
        var userRole = await _dbContext.Set<UserRole>().FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == teacherRoleId);

        if (userRole == null)
        {
            // If the user does not have the Teacher role, add it
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = teacherRoleId // Link to the Teacher role
            };

            _dbContext.Add(userRole);
            await _dbContext.SaveChangesAsync();
        }

        // Create and add the UserGroup entry linking the user to the group
        var userGroup = new UserRoleGroup
        {
            Id = Guid.NewGuid(),
            GroupId = newGroup.Id,
            UserRoleId = userRole.Id // Use the Teacher role ID
        };

        _dbContext.Add(userGroup);
        await _dbContext.SaveChangesAsync();

        // Fetch the created group including its owner
        var dbEntity = await _dbContext.Set<Group>()
                                        .Include(x => x.Owner)
                                        .FirstAsync(x => x.Id == newGroup.Id);

        // Generate the URL for the created group
        var url = Url.Action(nameof(Get), new { dbEntity.Id }) ?? throw new Exception("failed to generate url");
        return Created(url, dbEntity.ToDetail());
    }

    private async Task<string> GenerateUniqueCodeAsync(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        string code;

        do
        {
            code = new string(Enumerable.Repeat(chars, length)
                                        .Select(s => s[random.Next(s.Length)])
                                        .ToArray());
        }
        while (await _dbContext.Set<Group>().AnyAsync(x => x.Code == code));

        return code;
    }

    [HttpPatch("api/v1/Group/{id}")]

    public async Task<ActionResult<GroupDetailModel>> Update(
        [FromRoute] Guid id,
        [FromBody] JsonPatchDocument<GroupCreateModel> patch)
    {
        var dbEntity = await _dbContext
            .Set<Group>()
            .FirstOrDefaultAsync(x => x.Id == id);

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
            .Include(x => x.Rooms)
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

    [HttpPost("api/v1/Group/{code}/AddUser")]
    public async Task<ActionResult> AddCurrentUserToGroupByCode(
        [FromRoute] string code)
    {
        // Retrieve the group by its unique code
        var dbGroup = await _dbContext
            .Set<Group>()
            .FirstOrDefaultAsync(x => x.Code == code);

        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found with the provided code" });
        }

        // Get the current logged-in user's ID
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        // Check if the user already has the "Student" role in UserRole
        var studentRoleId = (await _dbContext.Set<Role>().FirstOrDefaultAsync(x => x.Name == "Student"))?.Id;

        if (studentRoleId == null)
        {
            return NotFound(new { Message = "Student role not found" });
        }

        // Fetch UserRole for this user and role
        var userRole = await _dbContext.Set<UserRole>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == studentRoleId);

        // If the user does not have the Student role, add it
        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = studentRoleId.Value // Link to the Student role
            };

            _dbContext.Add(userRole);
            await _dbContext.SaveChangesAsync();
        }

        // Check if the user is already a member of the group with the Student role
        var userGroupExists = await _dbContext.Set<UserRoleGroup>()
            .AnyAsync(ug => ug.GroupId == dbGroup.Id && ug.UserRoleId == userRole.Id);

        if (userGroupExists)
        {
            return Conflict(new { Message = "You are already a member of the group" });
        }

        // Create and add the UserGroup entry linking the user to the group
        var userGroup = new UserRoleGroup
        {
            Id = Guid.NewGuid(),
            GroupId = dbGroup.Id,
            UserRoleId = userRole.Id // Use the Student role ID
        };

        _dbContext.Add(userGroup);
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Successfully joined the group as a Student" });
    }
}
