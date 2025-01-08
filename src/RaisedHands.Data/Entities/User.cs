using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using RaisedHands.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace RaisedHands.Data.Entities;
[Table(name: "User")]

public class User : IdentityUser<Guid>, ITrackable
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public ICollection<UserRole> Roles { get; set; } = new HashSet<UserRole>();
    public Instant CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public Instant ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = null!;
    public Instant? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    private class Configuration : IEntityTypeConfiguration<User>
        {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("AspnNetUser");

            builder.HasData(SeedData());
        }
    }
    private static IEnumerable<User> SeedData()
    {
        yield return new User()
        {
            Id = new Guid("CEAB6921-DFED-4B4D-B661-DC36B8749067"),
            FirstName = "User",
            LastName = "Example",
            UserName = "user@example.com",
            NormalizedUserName = "USER@EXAMPLE.COM",
            Email = "user@example.com",
            NormalizedEmail = "USER@EXAMPLE.COM",
            EmailConfirmed = true,
            // Heslo@10
            PasswordHash = "AQAAAAEAACcQAAAAELKQmdGcfZbjxaz1GeqZ62mF7gEO9d49ofpdaQ+Mq0904MEIWvUnaMMfx9gJ27NmdQ==",
            SecurityStamp = "2MLDENGLJTQEITJVCJMIJJQOKXOUNSD6",
            ConcurrencyStamp = "ba46c7df-e2cf-469d-a17d-b653c50a0147",
            PhoneNumber = "123456798",
            PhoneNumberConfirmed = true,
            TwoFactorEnabled = false,
            LockoutEnd = DateTimeOffset.MinValue,
            LockoutEnabled = true,
            AccessFailedCount = 0,
        }.SetCreateBySystem(Instant.MinValue);
    }
}

