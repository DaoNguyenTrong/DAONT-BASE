using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccountRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");
        }
    }
}
