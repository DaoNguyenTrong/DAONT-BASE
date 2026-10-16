using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarterKit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPasswordChangeLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedPasswordChangeCount",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangeLockedUntil",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangeWindowStartedAt",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedPasswordChangeCount",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "PasswordChangeLockedUntil",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "PasswordChangeWindowStartedAt",
                table: "accounts");
        }
    }
}
