using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities
{
    public class Role: IdentityRole<Guid>
    {
        public ICollection<UserRole> Users { get; set; } = new HashSet<UserRole>();
    }
}
