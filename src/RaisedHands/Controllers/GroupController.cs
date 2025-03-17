using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using RaisedHands.Api.Models;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Models.Hands;
using RaisedHands.Api.Models.Questions;
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
    /// Retrieves all groups where the current user has an active role.
    /// </summary>
    /// <returns>A list of groups the user belongs to, or NotFound if none exist.</returns>
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
            .Where(group => group.DeletedAt == null
                && group.UserGroups.Any(ug => ug.UserRole != null
                    && ug.UserRole.UserId == userId
                    && ug.IsActive))
            .ToListAsync();

        return Ok(dbEntities.Select(x => x.ToSmall()).ToList());
    }

    /// <summary>
    /// Retrieves the details of a specific group by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the group.</param>
    /// <returns>Group details if found, otherwise NotFound.</returns>
    [HttpGet("api/v1/Group/{id}")]
    public async Task<ActionResult<GroupDetailModel>> Get([FromRoute] Guid id)
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

        return Ok(dbEntity.ToDetail());
    }

    /// <summary>
    /// Creates a new group and assigns the current user as the owner with the "Teacher" role.
    /// </summary>
    /// <param name="model">The group creation model.</param>
    /// <returns>The created group details.</returns>
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
            UserRoleId = userRole.Id,
            IsActive = true
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
    /// Changes the role of a user within a group.
    /// </summary>
    /// <param name="groupId">The ID of the group.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>HTTP 200 on success, or an error response on failure.</returns>
    [HttpPatch("api/v1/Group/{groupId}/User/{userId}/ChangeRole")]
    public async Task<IActionResult> ChangeUserRole([FromRoute] Guid groupId, [FromRoute] Guid userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var userRoleGroup = await _dbContext.Set<UserRoleGroup>()
                .Include(ug => ug.UserRole)
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserRole.UserId == userId);

            var group = await _dbContext.Set<Group>().FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound(new { Message = "Group not found." });
            if (group.OwnerId == userId) return BadRequest(new { Message = "The group owner cannot be assigned the student role." });

            if (userRoleGroup == null) return NotFound(new { Message = "User is not assigned to this group." });

            var studentRoleId = Guid.Parse("29d79252-1b53-4b92-a8dd-403d547fc3c4");
            var teacherRoleId = Guid.Parse("ddb9ab69-cedf-4531-a2fd-138969b4bdd3");
            var newRoleId = userRoleGroup.UserRole.RoleId == studentRoleId ? teacherRoleId : studentRoleId;

            var userRole = await _dbContext.Set<UserRole>().FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == newRoleId);
            if (userRole == null)
            {
                userRole = new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = newRoleId };
                _dbContext.Add(userRole);
                await _dbContext.SaveChangesAsync();
            }

            userRoleGroup.UserRoleId = userRole.Id;
            _dbContext.Update(userRoleGroup);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { Message = "User role switched successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { Message = "An error occurred while switching the user role", Error = ex.Message });
        }
    }

    /// <summary>
    /// Generates a unique alphanumeric code of the specified length.
    /// </summary>
    /// <param name="length">The desired length of the code.</param>
    /// <returns>A unique alphanumeric string.</returns>
    private async Task<string> GenerateUniqueCodeAsync(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        string code;

        do
        {
            code = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }
        while (await _dbContext.Set<Group>().AnyAsync(x => x.Code == code));

        return code;
    }

    /// <summary>
    /// Updates specific properties of a group using a JSON Patch document.
    /// </summary>
    /// <param name="id">The unique identifier of the group to update.</param>
    /// <param name="patch">The JSON Patch document containing the updates.</param>
    /// <returns>
    /// Returns the updated group details if successful.
    /// Returns NotFound if the group does not exist.
    /// Returns ValidationProblem if the update violates validation rules.
    /// </returns>
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
            ModelState.AddModelError<GroupCreateModel>(x => x.Name, "Name is not unique");
        }

        if (!(ModelState.IsValid && TryValidateModel(toUpdate)))
        {
            return ValidationProblem(ModelState);
        }

        dbEntity.Name = toUpdate.Name;
        await _dbContext.SaveChangesAsync();

        dbEntity = await _dbContext.Set<Group>().FirstAsync(x => x.Id == id);
        return Ok(dbEntity.ToDetail());
    }

    /// <summary>
    /// Soft deletes a group by marking it as deleted.
    /// </summary>
    /// <param name="id">The ID of the group to delete.</param>
    /// <returns>HTTP 204 if successful.</returns>
    [HttpDelete("api/v1/Group/{id}")]
    public async Task<ActionResult> Delete([FromRoute] Guid id)
    {
        var dbEntity = await _dbContext.Set<Group>().Include(x => x.Rooms).FilterDeleted().SingleOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null) return NotFound();

        foreach (var room in dbEntity.Rooms)
        {
            room.SetDeleteBySystem(_clock.GetCurrentInstant());
        }

        dbEntity.SetDeleteBySystem(_clock.GetCurrentInstant());
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("api/v1/Group/{code}/AddUser")]
    public async Task<ActionResult> AddCurrentUserToGroupByCode([FromRoute] string code)
    {
        var dbGroup = await _dbContext.Set<Group>().FirstOrDefaultAsync(x => x.Code == code);
        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found with the provided code" });
        }

        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        var userGroup = await _dbContext.Set<UserRoleGroup>()
                .Include(ug => ug.UserRole)
                .FirstOrDefaultAsync(ug => ug.GroupId == dbGroup.Id && ug.UserRole.UserId == userId);

        if (userGroup != null)
        {
            if (userGroup.IsActive)
            {
                return Conflict(new { Message = "You are already a member of the group" });
            }

            // Reactivate the user's membership if it was previously inactive
            userGroup.IsActive = true;
            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Successfully rejoined the group" });
        }

        var studentRoleId = (await _dbContext.Set<Role>().FirstOrDefaultAsync(x => x.Name == "Student"))?.Id;
        if (studentRoleId == null)
        {
            return NotFound(new { Message = "Student role not found" });
        }

        var userRole = await _dbContext.Set<UserRole>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == studentRoleId);
        if (userRole == null)
        {
            userRole = new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = studentRoleId.Value };
            _dbContext.Add(userRole);
            await _dbContext.SaveChangesAsync();
        }

        var userRoleGroup = new UserRoleGroup
        {
            Id = Guid.NewGuid(),
            GroupId = dbGroup.Id,
            UserRoleId = userRole.Id,
            IsActive = true
        };

        _dbContext.Add(userRoleGroup);
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Successfully joined the group as a Student" });
    }

    /// <summary>
    /// Removes a user from a group by setting their group membership as inactive.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user leaving the group.</param>
    /// <returns>HTTP 200 if successful, NotFound if the group or user is not found.</returns>
    [HttpPost("api/v1/Group/{groupId}/User/{userId}/Leave")]
    public async Task<ActionResult> LeaveGroupById([FromRoute] Guid groupId, [FromRoute] Guid userId)
    {
        var dbGroup = await _dbContext.Set<Group>().FirstOrDefaultAsync(x => x.Id == groupId);
        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found with the provided ID" });
        }

        var userGroup = await _dbContext.Set<UserRoleGroup>()
            .Include(ug => ug.UserRole)
            .FirstOrDefaultAsync(ug => ug.GroupId == groupId && ug.UserRole.UserId == userId);
        if (userGroup == null)
        {
            return NotFound(new { Message = "You are not a member of this group" });
        }

        userGroup.IsActive = false;
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Successfully left the group" });
    }

    /// <summary>
    /// Retrieves a list of rooms associated with a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>A list of RoomDetailModel objects if found, or NotFound if the group does not exist.</returns>
    [HttpGet("api/v1/Group/{groupId}/Rooms")]
    public async Task<ActionResult<IEnumerable<RoomDetailModel>>> GetRoomsByGroupId([FromRoute] Guid groupId)
    {
        var dbGroup = await _dbContext.Set<Group>()
            .Include(x => x.Rooms)
            .Include(x => x.UserGroups)
                .ThenInclude(x => x.UserRole)
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userRoleGroup = dbGroup.UserGroups.FirstOrDefault(x => x.UserRole.UserId == User.GetUserId());
        if (userRoleGroup == null)
        {
            return Forbid();
        }

        var identity = User.Identity as ClaimsIdentity;
        identity?.AddClaim(new Claim("UserGroupIdClaim", userRoleGroup.Id.ToString()));

        var roomDetails = dbGroup.Rooms
            .Where(r => r.DeletedAt == null)
            .Select(r => r.ToDetail())
            .ToList();

        return Ok(roomDetails);
    }

    /// <summary>
    /// Retrieves all questions and hands raised by a user in a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A list of questions and hands raised by the user, or NotFound if the user is not part of the group.</returns>
    [HttpGet("api/v1/Group/{groupId}/User/{userId}/QuestionsAndHandsRaised")]
    public async Task<ActionResult<UserQuestionsAndHandsRaisedModel>> GetQuestionsAndHandsRaisedByUserInGroup(
        [FromRoute] Guid groupId,
        [FromRoute] Guid userId)
    {
        var group = await _dbContext.Set<Group>()
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userGroup = group.UserGroups.FirstOrDefault(ug => ug.UserRole.User.Id == userId);
        if (userGroup == null)
        {
            return NotFound(new { Message = "User not part of the group" });
        }

        var questions = await _dbContext.Set<Question>()
            .Where(q => q.Room.GroupId == groupId && q.UserRoleGroup.UserRole.User.Id == userId)
            .Include(q => q.Room)
            .ToListAsync();

        var handsRaised = await _dbContext.Set<Hand>()
            .Where(hr => hr.UserRoleGroup.UserRole.User.Id == userId && hr.Room.GroupId == groupId)
            .Include(hr => hr.Room)
            .ToListAsync();

        var result = new UserQuestionsAndHandsRaisedModel
        {
            UserId = userId,
            FirstName = userGroup.UserRole.User.FirstName,
            LastName = userGroup.UserRole.User.LastName,
            QuestionsAsked = questions.Any() ? questions.Select(q => new QuestionModel
            {
                QuestionId = q.Id,
                Content = q.Text,
                DateAsked = q.SendAt,
                RoomName = q.Room.Name
            }).ToList() : null,
            HandsRaised = handsRaised.Any() ? handsRaised.Select(hr => new HandRaisedModel
            {
                HandRaisedId = hr.Id,
                RoomName = hr.Room.Name
            }).ToList() : null
        };

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a list of users belonging to a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>A list of users within the group, including their roles. Returns NotFound if the group does not exist.</returns>
    [HttpGet("api/v1/Group/{groupId}/Users")]
    public async Task<ActionResult<IEnumerable<GroupUserModel>>> GetUsersByGroupId([FromRoute] Guid groupId)
    {
        var dbGroup = await _dbContext
            .Set<Group>()
            .Include(x => x.UserGroups)
                .ThenInclude(x => x.UserRole)
                    .ThenInclude(x => x.User)
            .Include(x => x.UserGroups) // Explicitly include Role
                .ThenInclude(x => x.UserRole)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var groupUsers = dbGroup.UserGroups
            .Where(ug => ug.IsActive)
            .Select(ug => new GroupUserModel
            {
                UserId = ug.UserRole.User.Id,
                FirstName = ug.UserRole.User.FirstName,
                LastName = ug.UserRole.User.LastName,
                RoleId = ug.UserRole.Role.Id,
                RoleName = ug.UserRole.Role.Name
            })
            .ToList();

        return Ok(groupUsers);
    }

    /// <summary>
    /// Retrieves the role ID and name of a user within a specific group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The user's role within the group. Returns NotFound if the group or user is not found.</returns>
    [HttpGet("api/v1/Group/{groupId}/User/{userId}/Role")]
    public async Task<ActionResult<IdNameModel>> GetUserRoleInGroup(
        [FromRoute] Guid groupId,
        [FromRoute] Guid userId)
    {
        var group = await _dbContext
            .Set<Group>()
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.Role)  // Include Role
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.User)  // Include User
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userGroup = group.UserGroups.FirstOrDefault(ug => ug.UserRole.User.Id == userId);
        if (userGroup == null)
        {
            return NotFound(new { Message = "User not part of the group" });
        }

        var roleModel = new IdNameModel
        {
            Id = userGroup.UserRole.Role.Id,
            Name = userGroup.UserRole.Role.Name
        };

        return Ok(roleModel);
    }
}
