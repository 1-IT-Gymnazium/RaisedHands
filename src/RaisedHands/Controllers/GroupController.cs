using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Models.Rooms;
using RaisedHands.Api.Models.Users;
using RaisedHands.Api.Utils;
using RaisedHands.Data;
using RaisedHands.Data.Entities;
using RaisedHands.Data.Interfaces;
using System.Security.Claims;

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

    /// <summary>
    /// Retrieves a list of all groups, including their associated rooms and owners.
    /// </summary>
    /// <returns>A list of GroupDetailModel representing all groups.</returns>
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
    /// Retrieves all groups where the current user has a role.
    /// </summary>
    /// <returns>A list of GroupDetailModel objects representing the user's groups, 
    /// or a 404 response if no groups are found, or a 401 response for unauthorized access.</returns>
    [HttpGet("api/v1/Group/my-groups")]
    public async Task<ActionResult<IEnumerable<GroupSmallModel>>> GetUserGroups()
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        var dbEntities = await _dbContext
            .Set<Group>()
            .Include(x => x.Rooms)
            .Include(x => x.Owner)
            .Include(x => x.UserGroups)
            .ThenInclude(ug => ug.UserRole)
            .ThenInclude(ur => ur.User)
            .Where(group => group.UserGroups
                .Any(ug => ug.UserRole != null && ug.UserRole.UserId == userId))
            .ToListAsync();

        if (!dbEntities.Any())
        {
            return NotFound(new { Message = "No groups found for the user" });
        }

        var groupDetails = dbEntities.Select(x => x.ToSmall());

        return Ok(groupDetails);
    }

    /// <summary>
    /// Retrieves the details of a specific group by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the group.</param>
    /// <returns>The details of the specified group.</returns>
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
    /// Creates a new group with a unique code and links the current user as the owner with the "Teacher" role.
    /// </summary>
    /// <param name="model">The model containing details for the new group.</param>
    /// <returns>The details of the created group.</returns>
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
        }.SetCreateBy(User.GetEmail(), now);

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

        var userId = User.GetUserId();

        var teacherRoleId = (await _dbContext.Set<Role>().FirstAsync(x => x.Name == "Teacher")).Id;

        var userRole = await _dbContext.Set<UserRole>().FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == teacherRoleId);

        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = teacherRoleId
            };

            _dbContext.Add(userRole);
            await _dbContext.SaveChangesAsync();
        }

        var userGroup = new UserRoleGroup
        {
            Id = Guid.NewGuid(),
            GroupId = newGroup.Id,
            UserRoleId = userRole.Id
        };

        _dbContext.Add(userGroup);
        await _dbContext.SaveChangesAsync();

        var dbEntity = await _dbContext.Set<Group>()
                                        .Include(x => x.Owner)
                                        .FirstAsync(x => x.Id == newGroup.Id);

        var url = Url.Action(nameof(Get), new { dbEntity.Id }) ?? throw new Exception("failed to generate url");
        return Created(url, dbEntity.ToDetail());
    }

    /// <summary>
    /// Generates a unique alphanumeric code of the specified length. 
    /// Ensures the code does not already exist in the database by checking against the `Group` table.
    /// </summary>
    /// <param name="length">The desired length of the code (default is 6).</param>
    /// <returns>A unique alphanumeric code as a string.</returns>
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

    /// <summary>
    /// Updates specific properties of a group using a JSON Patch document.
    /// </summary>
    /// <param name="id">The unique identifier of the group to update.</param>
    /// <param name="patch">The JSON Patch document containing the updates.</param>
    /// <returns>The updated details of the group.</returns>
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

    /// <summary>
    /// Soft-deletes a group by marking it as deleted in the database.
    /// </summary>
    /// <param name="id">The unique identifier of the group to delete.</param>
    /// <returns>No content if successful.</returns>
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

    /// <summary>
    /// Adds the current user to a group using the group's unique code. 
    /// If the user does not have the "Student" role, it is assigned.
    /// </summary>
    /// <param name="code">The unique code of the group to join.</param>
    /// <returns>A confirmation message on successful addition.</returns>
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

        // Check if the user is already in the group (role does not matter)
        var isUserInGroup = await _dbContext.Set<UserRoleGroup>()
            .AnyAsync(ug => ug.GroupId == dbGroup.Id && ug.UserRole.UserId == userId);

        if (isUserInGroup)
        {
            return Conflict(new { Message = "You are already a member of the group" });
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

    /// <summary>
    /// Retrieves a list of room details for a specified group by its unique ID.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>A list of RoomDetailModel objects representing the group's rooms, or a 404 response if the group is not found.</returns>
    [HttpGet("api/v1/Group/{groupId}/Rooms")]
    public async Task<ActionResult<IEnumerable<RoomDetailModel>>> GetRoomsByGroupId(
    [FromRoute] Guid groupId)
    {
        var dbGroup = await _dbContext
            .Set<Group>()
            .Include(x => x.Rooms)
            .Include(x => x.UserGroups)
                .ThenInclude(x => x.UserRole)
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userRoleGroup = dbGroup.UserGroups.FirstOrDefault(x => x.UserRole.UserId == User.GetUserId());

        var identity = User.Identity as ClaimsIdentity; //?

        identity.AddClaim(new Claim("UserGroupIdClaim", userRoleGroup.Id.ToString())); //?

        var roomDetails = dbGroup.Rooms.Select(r => r.ToDetail()).ToList();

        return Ok(roomDetails);
    }
}
