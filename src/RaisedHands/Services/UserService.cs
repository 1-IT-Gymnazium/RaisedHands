using RaisedHands.Data;
using Microsoft.EntityFrameworkCore;

namespace RaisedHands.Api.Services;

public class UserService
{
    private readonly AppDbContext _dbContext;

    public UserService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsUserTeacherInGroup(Guid userId, Guid groupId)
    {
        Guid teacherRoleId = Guid.Parse("ddb9ab69-cedf-4531-a2fd-138969b4bdd3");

        var userGroup = await _dbContext.UserRoleGroups
            .Include(ug => ug.UserRole)
                .ThenInclude(ur => ur.Role)
            .Include(ug => ug.UserRole)
                .ThenInclude(us => us.User)
            .Where(ug => ug.UserRole.UserId == userId
                         && ug.GroupId == groupId
                         && ug.UserRole.RoleId == teacherRoleId)
            .FirstOrDefaultAsync();

        return userGroup != null;
    }
}
