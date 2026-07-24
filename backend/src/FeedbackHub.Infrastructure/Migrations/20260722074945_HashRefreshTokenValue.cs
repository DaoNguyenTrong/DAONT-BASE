using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedbackHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HashRefreshTokenValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows hold a plaintext token with no corresponding hash — they can never satisfy a
            // hash-based lookup again, and leaving them in place would collide on the new unique index once
            // they all default to the same empty TokenHash value below. Discarding them simply logs those
            // sessions out on next refresh, same as any other refresh-token rotation.
            migrationBuilder.Sql("DELETE FROM refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "refresh_tokens",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);
        }
    }
}
