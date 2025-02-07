using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace RaisedHands.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModifyQuestionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Answered",
                table: "Questions");

            migrationBuilder.RenameColumn(
                name: "DateTime",
                table: "Questions",
                newName: "SendAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnsweredAt",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    RequestInfo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "AnsweredAt",
                table: "Questions");

            migrationBuilder.RenameColumn(
                name: "SendAt",
                table: "Questions",
                newName: "DateTime");

            migrationBuilder.AddColumn<bool>(
                name: "Answered",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
