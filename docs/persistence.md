# Persistence notes

Background for backend persistence code whose reasoning doesn't fit a 3-line comment (`.claude/rules/comments.md`). Each section is linked from the code it explains.

## Explicit transactions — `IUnitOfWork.ExecuteInTransactionAsync`

Use it only when a business operation needs **several** `SaveChangesAsync` calls in a fixed order. Don't use it "to be safe": one `SaveChangesAsync` is already atomic, so put the changes into a single save whenever you can.

- **Why order needs separate saves.** EF sorts statements against the same table by key, not by the order they appear in the code. Two changes that a unique index forbids from coexisting can then be emitted in the wrong order and fail halfway. Examples: swapping two rows' unique values, or retiring the current row before inserting its replacement. Splitting them into separate saves fixes the order, and the transaction keeps them one unit.
- **Joining an open transaction.** If a transaction is already open (a caller's, or a test's rollback-only one), the operation joins it instead of opening another, and the outer owner decides whether to commit or roll back. Joining takes no savepoint, so a failed statement inside aborts the **whole** outer transaction. After that, Postgres answers every later statement with `25P02`, which means a caller can't catch an inner failure and carry on. Let the failure propagate.
- **After a rollback.** The change tracker still holds what the earlier saves wrote, as if it were in the database. Don't retry on the same unit of work; discard the scope and start from a fresh one.
- **Retrying execution strategies.** `EnableRetryOnFailure` rejects a user-opened transaction. If it is enabled, wrap the call in `CreateExecutionStrategy().ExecuteAsync`.

## `NormalizeAccountIdentity` migration — duplicate accounts

This migration changes data only, not the schema. It trims and lower-cases stored usernames and emails to the canonical form that `Account.NormalizeUsername`/`NormalizeEmail` apply on every write and lookup. Without it, an existing mixed-case row would never match its own normalized login.

- **It aborts startup on duplicates.** Migrations run in `Database.MigrateAsync`, so startup aborts when two accounts differ only by case or padding. Merging them silently would hand one person's data to another. The error message carries counts only, with no emails in the logs. To find the rows, run the query below, then do the same for `"Username"`:

  ```sql
  SELECT lower(btrim("Email") COLLATE "und-x-icu"), count(*)
  FROM accounts GROUP BY 1 HAVING count(*) > 1;
  ```

  Merge or rename the duplicates, then restart.

- **It is safe to re-run.** Rows that are already normalized don't match the `UPDATE` filters.
- **The ICU collation is explicit.** Plain `lower()` follows the database's ctype. Under a `C` locale it leaves non-ASCII letters (`É`) untouched, while the application's `ToLowerInvariant()` lowers them, so the row would never match its login. Every `lower()` therefore uses the ICU root collation, which requires a Postgres server built with ICU (the official images are).
