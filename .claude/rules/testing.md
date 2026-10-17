---
paths:
  - "backend/**"
  - "frontend/**"
---

# Testing Standards

What to test, at which layer, and how to write it. Test _infrastructure_ — Testcontainers collections, test-host isolation, `FastPasswordHasher`, Respawn, `setup.ts`, `storeIn`, router stubs, UTC fixtures — is in `commands.md` (§ Tests, § Frontend test conventions) and isn't repeated here.

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
- Build entities through their domain `Create()` factory (CLAUDE.md § Critical Invariants), never by hand or reflection.
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
