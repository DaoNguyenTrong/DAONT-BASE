using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarterKit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationScopingToFilesApiKeysAuditLogsSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_system_settings_Key",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_stored_files_OwnerId",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "stored_files");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "system_settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "stored_files",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "api_keys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_OrganizationId_Key",
                table: "system_settings",
                columns: new[] { "OrganizationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_OrganizationId",
                table: "stored_files",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_organization_id",
                table: "audit_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_organization_id",
                table: "api_keys",
                column: "organization_id");

            migrationBuilder.AddForeignKey(
                name: "FK_api_keys_organizations_organization_id",
                table: "api_keys",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stored_files_organizations_OrganizationId",
                table: "stored_files",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_system_settings_organizations_OrganizationId",
                table: "system_settings",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_api_keys_organizations_organization_id",
                table: "api_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_stored_files_organizations_OrganizationId",
                table: "stored_files");

            migrationBuilder.DropForeignKey(
                name: "FK_system_settings_organizations_OrganizationId",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_system_settings_OrganizationId_Key",
                table: "system_settings");

            migrationBuilder.DropIndex(
                name: "IX_stored_files_OrganizationId",
                table: "stored_files");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_organization_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_api_keys_organization_id",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "stored_files");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "api_keys");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "stored_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_Key",
                table: "system_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_OwnerId",
                table: "stored_files",
                column: "OwnerId");
        }
    }
}
