---
name: new-backend-slice
description: Scaffold a new backend entity slice (entity, service, DTOs, EF configuration, controller, tests) with the starterkit-entity dotnet template, then do the steps the template cannot. Use when adding a new entity or resource to backend/.
---

# new-backend-slice

The template writes the files; the steps below are what it cannot do. Do them in order. The tier of the work comes from
`bun scripts/risk-tier.ts` (a new permission or tenant filter is tier 2).

## Generate

```bash
dotnet new install backend/templates/starterkit-entity
dotnet new starterkit-entity -n Widget --plural Widgets --route widgets -o backend   # add --org-scoped false for a global entity
dotnet new uninstall backend/templates/starterkit-entity
```

Never pass `--force`: it would overwrite files you have already edited. Replace `Widget`, `Widgets` and `widgets` with
your names. The route with `-` turned into `_` becomes the table name.

## By hand, in order

1. **Permission.** In `backend/src/StarterKit.Application/Common/Authorization/Permissions.cs` add `<Plural>Manage = "<plural>.manage"` and list it
   in both `ActiveOrganizationScoped` and `All`. Without `ActiveOrganizationScoped` every call answers 403. Mirror it
   in `frontend/src/lib/permissions.ts` and `permission-label.ts` plus the `organizations.permission<Plural>Manage` label
   in both locales, or the role editor shows the raw code.
2. **DI.** In `backend/src/StarterKit.Application/DependencyInjection.cs` add the `using` and `services.AddScoped<IWidgetService, WidgetService>()`.
3. **Messages.** Add `<Name>NameRequired` and `<Name>NameTooLong` to
   `backend/src/StarterKit.Domain/Exceptions/DomainMessages.cs` and to both `Messages.resx` and `Messages.en.resx`
   (`MessageKeysTests` fails otherwise). Not-found uses the generic `EntityNotFound`.
4. **Migration**, after a build and before any API test, because the test host migrates the database:
   `commands.md` § EF Core Migrations.
5. **Contract.** `scripts/sync-contract.sh`, then commit `shared/openapi/openapi.json`. Declare the applicable 401/403 responses on the
   controller until an authorization-response transformer exists (`api-contract.md`).
6. **Business fields** beyond `Name`, with their validation in `Update` and their tests.
7. **`DbSet`** in `backend/src/StarterKit.Infrastructure/Persistence/AppDbContext.cs` if you want the convention; it is not required to build.
8. **Sensitive slice** (auth, tenant, permissions): add its paths to `TIER2_PATHS` in `scripts/risk-tier.ts`.

## Check

`backend/scripts/test.sh`. `ArchitectureTests`, `AnonymousAccessTests` and the generated tenant-isolation tests pick the
new controller up on their own. Rules: `.claude/rules/architecture.md`, `code-conventions.md`, `testing.md`.
