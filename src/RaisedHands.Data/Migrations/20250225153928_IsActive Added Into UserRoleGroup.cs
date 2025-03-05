using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaisedHands.Data.Migrations
{
    /// <inheritdoc />
    public partial class IsActiveAddedIntoUserRoleGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UserGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UserGroups");
        }
    }
}
