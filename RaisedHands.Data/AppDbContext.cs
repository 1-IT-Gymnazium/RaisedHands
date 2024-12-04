using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RaisedHands.Data.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace RaisedHands.Data;

public class AppDbContext : IdentityDbContext<User, Role, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{

    public DbSet<Room> Rooms { get; set; } = null!;

    public DbSet<Question> Questions { get; set; } = null!;

    public DbSet<Hand> Hands { get; set; } = null!;

    public DbSet<Group> Groups { get; set; } = null!;

    public DbSet<UserRoleGroup> UserGroups { get; set; } = null!;

    public DbSet<Email> Emails { get; set; } = null!;

    public AppDbContext(DbContextOptions options) : base(options)
    {

    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasKey("Id");

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

        modelBuilder.Entity<UserRoleGroup>()
            .HasOne(e => e.Group)
            .WithMany(e => e.UserGroups)
            .HasForeignKey(e => e.GroupId);

        modelBuilder.Entity<UserRoleGroup>()
            .HasOne(e => e.UserRole)
            .WithMany(e => e.UserGroups)
            .HasForeignKey(e => e.UserRoleId);

        modelBuilder.Entity<Role>().HasData(new Role
        { Id = Guid.Parse("74681C7E-6270-4ED1-8F2D-9347A326F974"), Name = "Admin" });

        modelBuilder.Entity<Role>().HasData(new Role
        { Id = Guid.Parse("29D79252-1B53-4B92-A8DD-403D547FC3C4"), Name = "Student" });

        modelBuilder.Entity<Role>().HasData(new Role
        { Id = Guid.Parse("DDB9AB69-CEDF-4531-A2FD-138969B4BDD3"), Name = "Teacher" });

        var assemblyWithConfiguration = GetType().Assembly;
        modelBuilder.ApplyConfigurationsFromAssembly(assemblyWithConfiguration);
    }
}
