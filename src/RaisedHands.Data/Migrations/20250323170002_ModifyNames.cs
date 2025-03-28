using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaisedHands.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModifyNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hands_UserGroups_UserRoleGroupId",
                table: "Hands");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_UserGroups_UserRoleGroupId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserGroups_AspNetUserRoles_UserRoleId",
                table: "UserGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_UserGroups_Groups_GroupId",
                table: "UserGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserGroups",
                table: "UserGroups");

            migrationBuilder.RenameTable(
                name: "UserGroups",
                newName: "UserRoleGroups");

            migrationBuilder.RenameIndex(
                name: "IX_UserGroups_UserRoleId",
                table: "UserRoleGroups",
                newName: "IX_UserRoleGroups_UserRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_UserGroups_GroupId",
                table: "UserRoleGroups",
                newName: "IX_UserRoleGroups_GroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoleGroups",
                table: "UserRoleGroups",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Hands_UserRoleGroups_UserRoleGroupId",
                table: "Hands",
                column: "UserRoleGroupId",
                principalTable: "UserRoleGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_UserRoleGroups_UserRoleGroupId",
                table: "Questions",
                column: "UserRoleGroupId",
                principalTable: "UserRoleGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoleGroups_AspNetUserRoles_UserRoleId",
                table: "UserRoleGroups",
                column: "UserRoleId",
                principalTable: "AspNetUserRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoleGroups_Groups_GroupId",
                table: "UserRoleGroups",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hands_UserRoleGroups_UserRoleGroupId",
                table: "Hands");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_UserRoleGroups_UserRoleGroupId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleGroups_AspNetUserRoles_UserRoleId",
                table: "UserRoleGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoleGroups_Groups_GroupId",
                table: "UserRoleGroups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoleGroups",
                table: "UserRoleGroups");

            migrationBuilder.RenameTable(
                name: "UserRoleGroups",
                newName: "UserGroups");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoleGroups_UserRoleId",
                table: "UserGroups",
                newName: "IX_UserGroups_UserRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoleGroups_GroupId",
                table: "UserGroups",
                newName: "IX_UserGroups_GroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserGroups",
                table: "UserGroups",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Hands_UserGroups_UserRoleGroupId",
                table: "Hands",
                column: "UserRoleGroupId",
                principalTable: "UserGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_UserGroups_UserRoleGroupId",
                table: "Questions",
                column: "UserRoleGroupId",
                principalTable: "UserGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserGroups_AspNetUserRoles_UserRoleId",
                table: "UserGroups",
                column: "UserRoleId",
                principalTable: "AspNetUserRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserGroups_Groups_GroupId",
                table: "UserGroups",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
