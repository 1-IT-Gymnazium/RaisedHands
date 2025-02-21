using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaisedHands.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModifyHandEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Answered",
                table: "Hands");

            migrationBuilder.RenameColumn(
                name: "DateTime",
                table: "Hands",
                newName: "SendAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnsweredAt",
                table: "Hands",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnsweredAt",
                table: "Hands");

            migrationBuilder.RenameColumn(
                name: "SendAt",
                table: "Hands",
                newName: "DateTime");

            migrationBuilder.AddColumn<bool>(
                name: "Answered",
                table: "Hands",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
