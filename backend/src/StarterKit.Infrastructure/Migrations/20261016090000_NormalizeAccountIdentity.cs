using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StarterKit.Infrastructure.Persistence;

#nullable disable

namespace StarterKit.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only migration (no schema change): brings stored usernames and emails to the canonical
    /// form <c>Account.NormalizeUsername</c>/<c>NormalizeEmail</c> now apply on every write and lookup
    /// (trimmed, lower-case). Without it, any existing mixed-case row would stop matching its own
    /// normalized login.
    /// </summary>
    /// <remarks>
    /// Aborts — and with it startup, since migrations run in <c>Database.MigrateAsync</c> — when two
    /// accounts differ only by case or padding: merging them silently would hand one person's data to
    /// another. The message carries counts only (no emails in logs); find the rows with
    /// <c>SELECT lower(btrim("Email") COLLATE "und-x-icu"), count(*) FROM accounts GROUP BY 1 HAVING count(*) > 1</c>
    /// (same for "Username"), merge or rename them, then restart. Safe to re-run: normalized rows no
    /// longer match the UPDATE filters.
    /// <para>
    /// Every <c>lower()</c> uses the ICU root collation explicitly: plain <c>lower()</c> follows the
    /// database's ctype, and under a <c>C</c> locale it leaves non-ASCII letters (<c>É</c>) untouched
    /// while the application's <c>ToLowerInvariant()</c> lowers them — the row would never match its
    /// login. Requires a server built with ICU (the official postgres images are).
    /// </para>
    /// </remarks>
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
