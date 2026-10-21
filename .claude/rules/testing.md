---
paths:
  - "backend/**"
  - "frontend/**"
---

# Testing Standards

What to test, at which layer, how to write it, and the test infrastructure that keeps the suite fast and isolated. Commands to run the tests: `commands.md` § Tests.

## When a change needs a test

A change isn't done until its tests are in the same PR. Pick the layer by what changed:

| Change                                                | Test it in                                      | Must cover                                                                                                                                   |
| ----------------------------------------------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Entity / domain rule (`Create`, `Update`)             | `Domain.Tests`                                  | every validation branch, valid and invalid                                                                                                   |
| Application service                                   | `Application.Tests`                             | the return value **and** the side effects (`Received(1)` / `DidNotReceive()`), every exception path                                          |
| EF query, repository, migration, raw SQL, Redis store | `Infrastructure.Tests`                          | the real SQL against Postgres (Testcontainers) — never EF InMemory                                                                           |
| Authorization handler, policy provider                | `Infrastructure.Tests`                          | the allow path and every deny reason                                                                                                         |
| Endpoint (route, DTO, status code, policy)            | `API.Tests`, over HTTP                          | happy path, 400 validation, **401 unauthenticated** for every `[Authorize]` endpoint, **403** for one with an organization/permission policy |
| Store, composable, `lib/` function                    | `frontend/tests/unit/{stores,composables,lib}/` | behavior plus edge cases — empty, null, boundaries, each error class                                                                         |
| Component, view, layout                               | `frontend/tests/{components,views,layouts}/`    | what the user sees and can do; HTTP mocked with MSW                                                                                          |
| Route meta, guard                                     | `frontend/tests/unit/router/`                   | the redirect for each meta flag                                                                                                              |
| API contract (controller, DTO)                        | —                                               | re-export `openapi.json` (`api-contract.md`); CI's drift check and `type-check` catch a stale contract and broken call sites                 |

**Bug fix:** write the regression test first and watch it fail for the reported reason, then fix. A fix without a test that would have caught the bug states why in the PR (e.g. only reproducible in a real browser).

**Not required on their own:** Mapperly mappers (covered through the service or endpoint using them), generated code (`frontend/src/api/generated/`), re-export files, settings POCOs.

## Writing backend tests

- Name `Method_Scenario_Expected` (`CreateAsync_DuplicateEmail_ThrowsConflict`). API tests name the endpoint (`Register_DuplicateUsername_ReturnsConflict`). A persistence test whose subject is the database rule itself may read as a sentence (`SameDedupeKeyForTheSameAccount_IsRejectedByTheDatabase`).
- xUnit `Assert` and NSubstitute only — no FluentAssertions, Shouldly or Moq.
- One private `Fixture` record per test class (the subject under test plus its substitutes), built by `CreateFixture()`; no base test classes.
- Build entities through their domain `Create()` factory (AGENTS.md § Critical Invariants), never by hand or reflection.
- Reuse the assertion helpers: `ApplicationAssert.ThrowsWithMessageAsync` / `AssertNotFoundAsync`, `DomainAssert`, and `RepositoryPredicateStub.StubFirstOrDefault` when a mocked repository must evaluate a real predicate.
- Assert exception and validation messages through their key constants (`DomainMessages.X`, resource keys), never literal text.
- Group methods with `// MethodName` section comments; no `// Arrange` / `// Act` / `// Assert`.
- Cases that differ only in data are one `[Theory]` with `[InlineData]`.

## Writing frontend tests

- The test file mirrors the source: `src/lib/x.ts` → `tests/unit/lib/x.test.ts`, `src/components/Y.vue` → `tests/components/Y.test.ts`.
- `describe('<component or function>')`, `it('<behavior, English, present tense>')` — `it('hides the item without the permission')`, not `it('should …')`.
- Mount with `renderComponent` (`tests/helpers/render.ts`), read stores with `storeIn`, build shared data with `tests/factories/`.
- HTTP is mocked with MSW only: `server.use(...)` inside the test. The global handlers in `tests/helpers/msw/handlers.ts` are only for background calls every page makes. `vi.mock` is for modules, not for HTTP.
- App-owned copy is asserted through the locale, never as a literal copied from `src/locales/en.ts`, so a copy change doesn't break tests: `import i18n from '@/plugins/i18n'`, then `i18n.global.t('auth.passwordTooShort', { min: PASSWORD_MIN_LENGTH })` — it fills placeholders. A mistyped key isn't a type error — vue-i18n returns the key itself, so the assertion fails at run time instead. A plain lookup on the imported `en` object (`en.auth.verifyEmailMissingToken`) is fine for text without placeholders. Literal text is fine for strings the test itself supplies (an MSW error `detail`) and in `e2e/`, which pins the locale to `en`.
- Query by role, label or text over CSS structure.

## Test infrastructure

### Backend

Test projects live under `backend/tests/` (`StarterKit.Domain.Tests`, `StarterKit.Application.Tests`, `StarterKit.Infrastructure.Tests`, `StarterKit.API.Tests`), mirroring the `backend/src/` layer split. `Infrastructure.Tests` and `API.Tests` each spin up their own `Testcontainers.PostgreSql` container — this is why they dominate total runtime (mostly container start-up) while `Domain.Tests`/`Application.Tests` are near-instant.

`Infrastructure.Tests` starts **one** Postgres container per process and migrates a template database once (`PostgresContainerFixture`); each xUnit collection (`PostgresCollection`, `PostgresCollectionB`) clones its own database from it (`CREATE DATABASE … TEMPLATE`), so collections run in parallel without seeing each other's data. A new Postgres-backed class that rolls back every test (`BeginTransactionAsync` in `InitializeAsync`) goes in `PostgresCollectionB`; one that commits rows or changes the schema goes in `PostgresCollection`. When one collection grows slow, add another `ICollectionFixture<PostgresContainerFixture>` definition and move classes over by measured time. Cleanup relies on Testcontainers' Ryuk — don't set `TESTCONTAINERS_RYUK_DISABLED`. The template migration runs with `Pooling=false`: an idle pooled connection left on the template makes every clone fail with `55006`.

- **Password hashing**: `ApiFactoryFixture` registers the real `PasswordHasher` at BCrypt cost 4 (`FastPasswordHasher.Create()`); production stays at 12, pinned by `PasswordHasherTests`. Seed hashes with `FastPasswordHasher.WorkFactor`, not a literal 12 — a cost-12 seed still verifies, it's just ~0.3s slower each time.
- **DB reset**: Respawn runs `DELETE`, not `TRUNCATE` (~60ms saved per reset). `DELETE` doesn't reset sequences — never assert a specific auto-increment id. The `hangfire` schema is excluded (wiping it makes every `WithWebHostBuilder` host reinstall the schema and hang).
- **No local config, no outbound calls**: the API test host reads the developer's gitignored `appsettings.json`, so `ApiFactoryFixture` pins every key of the committed `appsettings.Example.json` as an environment variable first (the same configuration CI runs with), then its own overrides — `CacheSettings:Provider=Memory` and `RealtimeSettings:Backplane=None` (Respawn only resets Postgres, so a dev Redis would carry state between runs), `StorageSettings:Provider=Local`, empty OAuth client ids and FCM credentials. `TestHostConfigurationTests` fails if the host resolves any pinned key differently. A new setting goes into the example file and is covered automatically; a new external service (HTTP API, SMTP, queue) gets an isolation override here or a stub registered once in `ConfigureTestServices` at its HTTP edge — never a test that passes because the real service happened to be reachable, or unreachable. A required base URL with no stub points at an `https://<name>.invalid` host, which can never resolve. Keys absent from the example still come from the local file — add them to the example rather than relying on that.
- **Real data (for forks)**: tests tagged `[Trait("Category", "RealData")]` read gitignored real source data and are **skipped by default** (none exist in the template yet — the tag is for a fork's import/reference-data checks, which only change outcome when that data does). Include them with `STARTERKIT_TEST_REAL_DATA=1 backend/scripts/test.sh` whenever that data changes; a plain `dotnet test` runs them, so they must skip themselves when the data file is absent.

### Frontend

- `tests/setup.ts` unmounts every wrapper after each test (`enableAutoUnmount`) so component timers can't fire into the next test. Keep it free of heavy imports — it runs once per test file; that's why the confirmation override lives in `lib/confirmation-override.ts`, not in naive-ui-backed `lib/feedback-naive.ts`.
- Read a store after `renderComponent` with `storeIn(pinia, useXxxStore)` (`tests/helpers/pinia.ts`), using the `pinia` it returns — a testing pinia ignores `useXxxStore(pinia)`'s argument and returns the *active* pinia's store, which a leftover timer from an earlier test may have switched.
- Tests that drive the real router (guards, route table) stub the lazy views they navigate to with `vi.mock('@/views/XxxView.vue', () => stubView())` (`tests/helpers/router-view-stubs.ts`) instead of loading real view chunks.
- **Time zones**: CI runs in UTC, developers usually don't. A fixture timestamp that the code parses in the machine's zone must be built from a UTC instant (`new Date('2026-09-15T12:00:00Z')`, then formatted the way the backend emits it), never hard-coded as local wall-clock text — that silently assumes the author's zone and fails on the runner. Check with `TZ=UTC bun run --cwd frontend test:run` (and `TZ=UTC backend/scripts/test.sh`) when a test touches dates.
- `type-check` runs `src` under `tsconfig.app.json` as well as `tsconfig.vitest.json` on purpose: the vitest config adds `node`/`vitest/globals` types, so only the app pass catches app code that leans on them.

## E2E (Playwright)

The specs drive a real backend with seeded accounts (`frontend/.env.e2e`), so they don't run in CI. Add or update a spec when a change alters a cross-page flow — login, logout, guards, the session list — and run `bun run --cwd frontend test:e2e` locally before asking for review of that change.

## Coverage

Measured and reported, not gated. CI writes backend and frontend coverage to each run's summary; locally run `STARTERKIT_TEST_COVERAGE=1 backend/scripts/test.sh` or `bun run --cwd frontend test:coverage`.

Baseline, measured 2026-10-17 (generated code excluded — EF migrations and compiler-generated classes on the backend, `src/api/generated/` and `src/typings/` on the frontend):

| Side     | Lines | Branches |
| -------- | ----- | -------- |
| Backend  | 91.8% | 78.4%    |
| Frontend | 80.6% | 73.1%    |

There's no fail-under threshold yet. Once a few PRs show the numbers are stable, set a floor just under the baseline and only ever raise it. Coverage finds untested code; a covered line whose outcome no assertion checks doesn't count as tested.

## Flaky tests

A test that fails intermittently is fixed or deleted in the next PR — never retried in CI, never skipped without a tracked reason. The usual causes here: a leaked timer (`enableAutoUnmount`), a local-time fixture, a Postgres class in the wrong collection, a host reaching a real service.
