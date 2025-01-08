using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities;

public class UserRoleGroup
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid UserRoleId { get; set; }
    public UserRole UserRole { get; set; } = null!;
}
