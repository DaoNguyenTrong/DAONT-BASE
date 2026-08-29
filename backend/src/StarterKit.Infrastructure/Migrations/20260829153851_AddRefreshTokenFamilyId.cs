using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarterKit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamilyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add nullable first, then backfill each existing row with its OWN family
            // id before enforcing NOT NULL. A static default would collapse every
            // historical token into one family and make reuse detection misfire
            // across unrelated sessions. New rows always get their FamilyId from the
            // domain factory, so the column keeps no default.
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE refresh_tokens SET \"FamilyId\" = gen_random_uuid() WHERE \"FamilyId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "FamilyId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_AccountId_FamilyId",
                table: "refresh_tokens",
                columns: new[] { "AccountId", "FamilyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_AccountId_FamilyId",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "refresh_tokens");
        }
    }
}
