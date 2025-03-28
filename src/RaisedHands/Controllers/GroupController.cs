using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using RaisedHands.Api.Models;
using RaisedHands.Api.Models.Groups;
using RaisedHands.Api.Models.Rooms;
using RaisedHands.Api.Models.Users;
using RaisedHands.Api.Services;
using RaisedHands.Api.Utils;
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
    private readonly UserService _userService;

    public GroupController(
        IClock clock,
        AppDbContext dbContext,
        UserService userService
        )
    {
        _clock = clock;
        _dbContext = dbContext;
        _userService = userService;
    }

    /// <summary>
    /// Gets all groups where the authenticated user is an active member.
    /// </summary>
    /// <remarks>
    /// A user is considered an active member if they have a non-deleted, active role assignment in the group.
    /// </remarks>
    /// <returns>
    /// 200 OK with a list of <see cref="GroupSmallModel"/> if groups are found.
    /// 401 Unauthorized if the user is not authenticated.
    /// </returns>
    [HttpGet("api/v1/Group/MyGroups")]
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
            .Include(x => x.UserRoleGroups)
            .ThenInclude(ug => ug.UserRole)
            .ThenInclude(ur => ur.User)
            .Where(group => group.DeletedAt == null
                && group.UserRoleGroups.Any(ug => ug.UserRole != null
                    && ug.UserRole.UserId == userId
                    && ug.IsActive))
            .ToListAsync();

        return Ok(dbEntities.Select(x => x.ToSmall()).ToList());
    }

    /// <summary>
    /// Creates a new group and assigns the authenticated user as the owner and a teacher.
    /// </summary>
    /// <param name="model">Model containing the group name.</param>
    /// <returns>
    /// 201 Created with a <see cref="GroupDetailModel"/> if successful.
    /// 400 Bad Request if the model is invalid or name is not unique.
    /// </returns>
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
    /// Switches the user's role between Teacher and Student within a specified group.
    /// </summary>
    /// <param name="groupId">Unique identifier of the group.</param>
    /// <param name="userId">Unique identifier of the target user.</param>
    /// <returns>
    /// 200 OK if the role was changed successfully.<br/>
    /// 400 Bad Request if the user is the group owner and cannot be downgraded.<br/>
    /// 404 Not Found if the group or user role assignment does not exist.<br/>
    /// 500 Internal Server Error on unexpected failure.
    /// </returns>
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
    /// Generates a unique alphanumeric code not currently used by any existing group.
    /// </summary>
    /// <param name="length">The desired length of the generated code. Defaults to 6 characters.</param>
    /// <returns>
    /// A unique uppercase alphanumeric string suitable for use as a group join code.
    /// </returns>
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
    /// <param name="patch">The JSON Patch document containing changes for <see cref="GroupCreateModel"/>.</param>
    /// <returns>
    /// 200 OK with updated <see cref="GroupDetailModel"/> if successful.<br/>
    /// 400 Bad Request if the update fails validation.<br/>
    /// 404 Not Found if the group does not exist.
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
    /// Soft deletes a group and all its associated rooms.
    /// </summary>
    /// <param name="id">ID of the group to delete.</param>
    /// <returns>
    /// 204 No Content on success.<br/>
    /// 401 Unauthorized if user is not logged in.<br/>
    /// 403 Forbidden if the user is not a teacher in the group.<br/>
    /// 404 Not Found if the group doesn't exist.
    /// </returns>
    [HttpDelete("api/v1/Group/{id}")]
    public async Task<ActionResult> Delete([FromRoute] Guid id)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { Message = "Invalid or unauthorized user" });
        }

        var dbEntity = await _dbContext
            .Set<Group>()
            .Include(x => x.Rooms)
            .FilterDeleted()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (dbEntity == null)
        {
            return NotFound();
        }

        bool isTeacher = await _userService.IsUserTeacherInGroup(userId, id);
        if (!isTeacher)
        {
            return Forbid("Only teachers in this group can delete it.");
        }

        foreach (var room in dbEntity.Rooms)
        {
            room.SetDeleteBySystem(_clock.GetCurrentInstant());
        }

        dbEntity.SetDeleteBySystem(_clock.GetCurrentInstant());
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Adds the authenticated user to a group using the group join code.
    /// </summary>
    /// <param name="code">The group's join code.</param>
    /// <returns>
    /// 200 OK on successful join or rejoin.<br/>
    /// 401 Unauthorized if user ID is invalid.<br/>
    /// 404 Not Found if the group or the "Student" role is not found.<br/>
    /// </returns>
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

        var studentRole = await _dbContext.Set<Role>().FirstOrDefaultAsync(x => x.Name == "Student");

        var userRoles = await _dbContext.Set<UserRole>().Where(x => x.UserId == userId).ToListAsync();

        UserRole chosenRole = null;

        if (studentRole != null)
        {
            chosenRole = userRoles.FirstOrDefault(x => x.RoleId == studentRole.Id);
        }

        if (chosenRole == null && userRoles.Any())
        {
            chosenRole = userRoles.First();
        }

        if (chosenRole == null)
        {
            if (studentRole == null)
            {
                return NotFound(new { Message = "Student role not found" });
            }

            chosenRole = new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = studentRole.Id };
            _dbContext.Add(chosenRole);
            await _dbContext.SaveChangesAsync();
        }

        var userGroup = await _dbContext.Set<UserRoleGroup>()
            .Include(ug => ug.UserRole)
            .FirstOrDefaultAsync(ug => ug.GroupId == dbGroup.Id && ug.UserRole.UserId == userId);

        if (userGroup != null)
        {
            userGroup.UserRoleId = chosenRole.Id;
            userGroup.IsActive = true;
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Successfully rejoined the group" });
        }

        var userRoleGroup = new UserRoleGroup
        {
            Id = Guid.NewGuid(),
            GroupId = dbGroup.Id,
            UserRoleId = chosenRole.Id,
            IsActive = true
        };

        _dbContext.Add(userRoleGroup);
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Successfully joined the group" });
    }

    /// <summary>
    /// Removes a user from a group by deactivating their group membership.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>
    /// 200 OK on success.<br/>
    /// 400 Bad Request if the user is the group owner.<br/>
    /// 404 Not Found if group or user membership is not found.
    /// </returns>
    [HttpPost("api/v1/Group/{groupId}/User/{userId}/Leave")]
    public async Task<ActionResult> LeaveGroupById([FromRoute] Guid groupId, [FromRoute] Guid userId)
    {
        var dbGroup = await _dbContext.Set<Group>().FirstOrDefaultAsync(x => x.Id == groupId);
        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found with the provided ID" });
        }

        if (dbGroup.OwnerId == userId)
        {
            return BadRequest(new { Message = "The group owner cannot leave the group." });
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
    /// <param name="groupId">The group ID.</param>
    /// <returns>
    /// 200 OK with a list of <see cref="RoomDetailModel"/>.<br/>
    /// 403 Forbidden if user is not a group member.<br/>
    /// 404 Not Found if the group does not exist.
    /// </returns>
    [HttpGet("api/v1/Group/{groupId}/Rooms")]
    public async Task<ActionResult<IEnumerable<RoomDetailModel>>> GetRoomsByGroupId([FromRoute] Guid groupId)
    {
        var dbGroup = await _dbContext.Set<Group>()
            .Include(x => x.Rooms)
            .Include(x => x.UserRoleGroups)
                .ThenInclude(x => x.UserRole)
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userRoleGroup = dbGroup.UserRoleGroups.FirstOrDefault(x => x.UserRole.UserId == User.GetUserId());
        if (userRoleGroup == null)
        {
            return Forbid();
        }

        var roomDetails = dbGroup.Rooms
            .Where(r => r.DeletedAt == null)
            .Select(r => r.ToDetail())
            .ToList();

        return Ok(roomDetails);
    }

    /// <summary>
    /// Retrieves all questions and hands raised by a user in a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="userId">The user's ID.</param>
    /// <returns>
    /// 200 OK with <see cref="UserQuestionsAndHandsRaisedModel"/>.<br/>
    /// 404 Not Found if the group or user is not associated.
    /// </returns>
    [HttpGet("api/v1/Group/{groupId}/User/{userId}/QuestionsAndHandsRaised")]
    public async Task<ActionResult<UserQuestionsAndHandsRaisedModel>> GetQuestionsAndHandsRaisedByUserInGroup(
        [FromRoute] Guid groupId,
        [FromRoute] Guid userId)
    {
        var group = await _dbContext.Set<Group>()
            .Include(g => g.UserRoleGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userGroup = group.UserRoleGroups.FirstOrDefault(ug => ug.UserRole.User.Id == userId);
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

        var result = userGroup.ToUserQuestionsAndHandsRaisedModel(userId, questions, handsRaised);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a list of users within a specific group and their roles.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>
    /// 200 OK with a list of <see cref="GroupUserModel"/>.<br/>
    /// 404 Not Found if the group is not found.
    /// </returns>
    [HttpGet("api/v1/Group/{groupId}/Users")]
    public async Task<ActionResult<IEnumerable<GroupUserModel>>> GetUsersByGroupId([FromRoute] Guid groupId)
    {
        var dbGroup = await _dbContext
            .Set<Group>()
            .Include(x => x.UserRoleGroups)
                .ThenInclude(x => x.UserRole)
                    .ThenInclude(x => x.User)
            .Include(x => x.UserRoleGroups)
                .ThenInclude(x => x.UserRole)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == groupId);

        if (dbGroup == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var groupUsers = dbGroup.UserRoleGroups
            .Where(ug => ug.IsActive)
            .Select(ug => ug.ToGroupUserModel())
            .ToList();

        return Ok(groupUsers);
    }

    /// <summary>
    /// Gets the role assigned to a specific user in a group.
    /// </summary>
    /// <param name="groupId">Group identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <returns>
    /// 200 OK with <see cref="IdNameModel"/> of role ID and name.<br/>
    /// 404 Not Found if the group or user is not found.
    /// </returns>
    [HttpGet("api/v1/Group/{groupId}/User/{userId}/Role")]
    public async Task<ActionResult<IdNameModel>> GetUserRoleInGroup(
        [FromRoute] Guid groupId,
        [FromRoute] Guid userId)
    {
        var group = await _dbContext
            .Set<Group>()
            .Include(g => g.UserRoleGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.Role)
            .Include(g => g.UserRoleGroups)
                .ThenInclude(ug => ug.UserRole)
                    .ThenInclude(ur => ur.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            return NotFound(new { Message = "Group not found" });
        }

        var userGroup = group.UserRoleGroups.FirstOrDefault(ug => ug.UserRole.User.Id == userId);
        if (userGroup == null)
        {
            return NotFound(new { Message = "User not part of the group" });
        }

        var roleModel = new UserRoleGroupModel
        {
            Id = userGroup.UserRole.Role.Id,
            Name = userGroup.UserRole.Role.Name,
            IsActive = userGroup.IsActive
        };

        return Ok(roleModel);
    }
}
