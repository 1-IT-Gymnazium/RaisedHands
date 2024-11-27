using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities;

public  class UserRole:IdentityUserRole<Guid>
{
    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
