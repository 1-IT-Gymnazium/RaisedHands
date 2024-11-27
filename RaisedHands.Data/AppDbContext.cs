using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RaisedHands.Data.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace RaisedHands.Data;

public class AppDbContext : IdentityDbContext<User,Role, Guid>
{

    public DbSet<Room> Rooms { get; set; } = null!;

    public DbSet<Question> Questions { get; set; } = null!;

    public DbSet<Hand> Hands { get; set; } = null!;

    public DbSet<Group> Groups { get; set; } = null!;

    public DbSet<UserGroup> UserGroups { get; set; } = null!;

    public DbSet<Email> Emails { get; set; } = null!;

    public AppDbContext(DbContextOptions options) : base(options)
    {

    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasOne(e => e.Role)
            .WithMany(e => e.Users)
            .HasForeignKey(e => e.RoleId)
            .HasPrincipalKey(e => e.Id);
        modelBuilder.Entity<UserRole>()
            .HasOne(e => e.User)
            .WithMany(e => e.Roles)
            .HasForeignKey(e => e.UserId)
            .HasPrincipalKey(e => e.Id);

        modelBuilder.Entity<Role>().HasData(new Role
        { Id = Guid.Parse("74681C7E-6270-4ED1-8F2D-9347A326F974"), Name = "Admin" });

        var assemblyWithConfiguration = GetType().Assembly;
        modelBuilder.ApplyConfigurationsFromAssembly(assemblyWithConfiguration);
    }
}
