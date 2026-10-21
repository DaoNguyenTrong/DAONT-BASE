using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StarterKit.Infrastructure.Persistence;

#nullable disable

namespace StarterKit.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only, re-runnable: trims and lower-cases stored usernames/emails to the form <c>Account.Normalize*</c>
    /// applies. Aborts startup when two accounts differ only by case or padding — finding and resolving them, and why
    /// every <c>lower()</c> pins the ICU collation: docs/persistence.md § NormalizeAccountIdentity.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016090000_NormalizeAccountIdentity")]
    public partial class NormalizeAccountIdentity : Migration
    {
        public const string UpSql = """
            DO $$
            DECLARE
                dup_users int;
                dup_emails int;
            BEGIN
                SELECT count(*) INTO dup_users FROM (
                    SELECT 1 FROM accounts
                    GROUP BY lower(btrim("Username") COLLATE "und-x-icu")
                    HAVING count(*) > 1) d;

                SELECT count(*) INTO dup_emails FROM (
                    SELECT 1 FROM accounts
                    GROUP BY lower(btrim("Email") COLLATE "und-x-icu")
                    HAVING count(*) > 1) d;

                IF dup_users > 0 OR dup_emails > 0 THEN
                    RAISE EXCEPTION 'NormalizeAccountIdentity: % username / % email group(s) differ only by case or padding. Merge or rename them, then restart.', dup_users, dup_emails;
                END IF;

                UPDATE accounts SET "Username" = lower(btrim("Username") COLLATE "und-x-icu")
                 WHERE "Username" <> lower(btrim("Username") COLLATE "und-x-icu");

                UPDATE accounts SET "Email" = lower(btrim("Email") COLLATE "und-x-icu")
                 WHERE "Email" <> lower(btrim("Email") COLLATE "und-x-icu");
            END $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Original casing is not recoverable; lower-case values remain valid for the old code.
        }
    }
}
