using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarterKit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SystemRoleKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roles_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_member_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_member_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_member_roles_organization_members_Organization~",
                        column: x => x.OrganizationMemberId,
                        principalTable: "organization_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_member_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_member_roles_OrganizationMemberId_RoleId",
                table: "organization_member_roles",
                columns: new[] { "OrganizationMemberId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_member_roles_RoleId",
                table: "organization_member_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_RoleId_PermissionCode",
                table: "role_permissions",
                columns: new[] { "RoleId", "PermissionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_OrganizationId_Name",
                table: "roles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_OrganizationId_SystemRoleKind",
                table: "roles",
                columns: new[] { "OrganizationId", "SystemRoleKind" },
                unique: true,
                filter: "\"SystemRoleKind\" IS NOT NULL");

            // Backfill: seed the 3 system roles for every existing organization, give Admin its
            // default permission, and map each existing organization_members.Role value to the
            // matching system role of the same org — all while the old "Role" column still exists.
            migrationBuilder.Sql(
                """
                INSERT INTO roles ("Id", "OrganizationId", "Name", "SystemRoleKind", "CreatedAt")
                SELECT gen_random_uuid(), o."Id", v."Name", v."Kind", now()
                FROM organizations o
                CROSS JOIN (VALUES ('Owner', 'Owner'), ('Admin', 'Admin'), ('Member', 'Member')) AS v("Name", "Kind");

                INSERT INTO role_permissions ("Id", "RoleId", "PermissionCode", "CreatedAt")
                SELECT gen_random_uuid(), r."Id", 'organizations.members.manage', now()
                FROM roles r
                WHERE r."SystemRoleKind" = 'Admin';

                INSERT INTO organization_member_roles ("Id", "OrganizationMemberId", "RoleId", "CreatedAt")
                SELECT gen_random_uuid(), om."Id", r."Id", now()
                FROM organization_members om
                JOIN roles r ON r."OrganizationId" = om."OrganizationId" AND r."SystemRoleKind" = om."Role";
                """);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "organization_members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_member_roles");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "organization_members",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
