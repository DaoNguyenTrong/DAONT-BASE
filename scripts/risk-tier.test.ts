import { expect, test } from 'bun:test'
import {
  contentLines,
  fixRatioByTier,
  formatFixRatio,
  splitNul,
  tierOf,
  tierOfChange,
  type CommitInfo,
  type Tier,
} from './risk-tier'

test.each<[string, Tier]>([
  ['docs/process/lifecycle.md', 0],
  ['frontend/src/locales/vi.ts', 0],
  ['shared/openapi/openapi.json', 0],
  ['frontend/src/api/generated/products.ts', 0],
  ['.claude/agents/implementer.md', 0],
  ['frontend/tests/unit/views/products.test.ts', 0],
  ['frontend/src/views/products/ProductList.vue', 1],
  ['backend/src/StarterKit.Application/Services/Products/ProductService.cs', 1],
  ['scripts/check-comments.ts', 1],
  ['backend/src/StarterKit.Application/Services/Auth/TokenIssuer.cs', 2],
  ['backend/src/StarterKit.API/Common/AuthCookies.cs', 2],
  ['backend/src/StarterKit.API/Middleware/TenantAccessMiddleware.cs', 2],
  ['backend/src/StarterKit.Infrastructure/Persistence/Migrations/20261112_X.cs', 2],
  ['backend/src/StarterKit.API/Program.cs', 2],
  ['frontend/src/api/client.ts', 2],
  ['frontend/src/router/index.ts', 2],
  ['.github/workflows/ci.yml', 2],
  ['lefthook.yml', 2],
  ['scripts/risk-tier.ts', 2],
])('tierOf(%s) = %d', (path, tier) => expect(tierOf(path)).toBe(tier))

test('tierOf_authDirectoryWithoutKeywordInName_isTier2', () =>
  expect(tierOf('backend/src/StarterKit.Application/Services/Auth/LoginFlow.cs')).toBe(2))

test('tierOf_testUnderSensitivePath_isTier1', () =>
  expect(tierOf('backend/tests/StarterKit.Application.Tests/Services/Auth/AuthServiceTests.cs')).toBe(1))

test('tierOf_lowercaseKeyword_isTier2', () => expect(tierOf('frontend/src/stores/auth.ts')).toBe(2))

test('tierOf_tierScriptTest_isTier1', () => expect(tierOf('scripts/risk-tier.test.ts')).toBe(1))

test('tierOfChange_takesTheMaxAndNamesTheReason', () => {
  const r = tierOfChange([
    { path: 'docs/a.md', changedLines: [] },
    { path: 'frontend/src/api/client.ts', changedLines: [] },
  ])
  expect(r.tier).toBe(2)
  expect(r.reasons[0]).toStartWith('frontend/src/api/client.ts')
})

test('tierOfChange_allowAnonymousAddedInTier1File_isTier2', () =>
  expect(
    tierOfChange([
      {
        path: 'backend/src/StarterKit.API/Controllers/ProductsController.cs',
        changedLines: ['    [AllowAnonymous]'],
      },
    ]).tier,
  ).toBe(2))

test('tierOfChange_markerInDoc_staysTier0', () =>
  expect(
    tierOfChange([{ path: 'docs/tech/01.md', changedLines: ['Dùng [Authorize] cho mọi action'] }]).tier,
  ).toBe(0))

test('tierOfChange_markerInTest_staysAtItsPathTier', () =>
  expect(
    tierOfChange([{ path: 'frontend/tests/unit/x.test.ts', changedLines: ['withCredentials: true'] }]).tier,
  ).toBe(0))

test('tierOfChange_empty_isTier0', () => expect(tierOfChange([]).tier).toBe(0))

test('tierOfChange_reasonsCappedAtFive', () => {
  const files = Array.from({ length: 8 }, (_, i) => ({ path: `frontend/src/router/r${i}.ts`, changedLines: [] }))
  expect(tierOfChange(files).reasons).toHaveLength(5)
})

const commit = (subject: string, ...paths: string[]): CommitInfo => ({
  subject,
  files: paths.map((path) => ({ path, changedLines: [] })),
})

test('fixRatioByTier_countsFixSubjectsPerTier', () =>
  expect(
    fixRatioByTier([
      commit('fix(auth): keep remember-me on refresh', 'frontend/src/api/client.ts'),
      commit('feat(auth): add switch', 'frontend/src/router/index.ts'),
      commit('feat(products): list', 'frontend/src/views/products/List.vue'),
    ]),
  ).toEqual({ 0: { fix: 0, total: 0 }, 1: { fix: 0, total: 1 }, 2: { fix: 1, total: 2 } }))

test('fixRatioByTier_fixPatternIsStrict', () => {
  const vue = 'frontend/src/views/A.vue'
  const r = fixRatioByTier(
    ['fix: a', 'fix(x): a', 'fix!: a', 'fixture: a', 'Revert "fix: a"'].map((s) => commit(s, vue)),
  )
  expect(r[1]).toEqual({ fix: 3, total: 5 })
})

test('fixRatioByTier_skipsCommitsWithNoFiles', () =>
  expect(fixRatioByTier([commit('fix: empty')])[0]).toEqual({ fix: 0, total: 0 }))

test('formatFixRatio_printsOneRowPerTierWithWholePercentOrDash', () =>
  expect(formatFixRatio({ 0: { fix: 0, total: 0 }, 1: { fix: 1, total: 3 }, 2: { fix: 2, total: 4 } })).toBe(
    ['| Hạng | fix | Tổng | Tỉ lệ |', '|---|---|---|---|', '| 0 | 0 | 0 | — |', '| 1 | 1 | 3 | 33% |', '| 2 | 2 | 4 | 50% |'].join(
      '\n',
    ),
  ))

// Final review: security files whose names carry no auth keyword.
test.each<[string, Tier]>([
  ['backend/src/StarterKit.API/Middleware/CsrfProtectionMiddleware.cs', 2],
  ['backend/src/StarterKit.Application/Common/Settings/JwtSettings.cs', 2],
  ['backend/src/StarterKit.Infrastructure/Services/Security/DataProtectionSecretProtector.cs', 2],
  ['backend/src/StarterKit.Infrastructure/Services/Notifications/NotificationHub.cs', 2],
  ['backend/src/StarterKit.Application/Common/Settings/CorsSettings.cs', 2],
  ['backend/src/StarterKit.Application/Common/Settings/RateLimiterSettings.cs', 2],
  ['backend/src/StarterKit.Application/Common/Settings/ForwardedHeadersSettings.cs', 2],
  ['backend/src/StarterKit.Application/Common/PasswordPolicy.cs', 2],
  ['backend/src/StarterKit.Application/Common/Interfaces/ICurrentUserService.cs', 2],
  ['frontend/src/lib/permissions.ts', 2],
  ['frontend/src/lib/auth-session.ts', 2],
  ['frontend/src/composables/use-organization-switch.ts', 2],
  ['.claude/settings.json', 2],
  ['.codex/hooks.json', 2],
  ['docker-compose.yml', 2],
  ['backend/Dockerfile', 2],
  ['backend/src/StarterKit.API/Properties/launchSettings.json', 2],
  ['scripts/test-gate.sh', 2],
  ['scripts/claude-bash-guard.ts', 2],
  ['frontend/package.json', 2],
  ['frontend/bun.lock', 2],
  ['backend/src/StarterKit.API/StarterKit.API.csproj', 2],
  ['backend/src/StarterKit.Application/DependencyInjection.cs', 1],
  ['backend/src/StarterKit.Infrastructure/Persistence/StarterKitDbContextFactory.cs', 1],
])('tierOf_securityFileWithoutAuthWord(%s) = %d', (path, tier) => expect(tierOf(path)).toBe(tier))

test.each([
  '        .RequireAuthorization();',
  '    app.UseAuthorization();',
  '    [Authorize(Roles = "Admin")]',
  '    if (User.IsInRole("Admin"))',
  '        .AddJwtBearer(options => {})',
  '        SameSite = SameSiteMode.None,',
  '        HttpOnly = false,',
  '        Secure = false,',
  '    services.AddCors();',
  "  localStorage.setItem('access_token', token)",
  '  document.cookie = value',
  "  fetch(url, { credentials: 'include' })",
])('tierOfChange_securityMarker(%s)_isTier2', (line) =>
  expect(tierOfChange([{ path: 'backend/src/StarterKit.API/Controllers/ProductsController.cs', changedLines: [line] }]).tier).toBe(2))

test('tierOfChange_testWithRemovedLines_isTier1', () =>
  expect(
    tierOfChange([
      { path: 'backend/tests/StarterKit.API.Tests/ProductsControllerTests.cs', changedLines: ['Assert.Equal(403, s)'], removedLines: ['Assert.Equal(403, s)'] },
    ]).tier,
  ).toBe(1))

test('tierOfChange_testWithOnlyAddedLines_staysTier0', () =>
  expect(
    tierOfChange([
      { path: 'backend/tests/StarterKit.API.Tests/ProductsControllerTests.cs', changedLines: ['Assert.True(x)'], removedLines: [] },
    ]).tier,
  ).toBe(0))

test('contentLines_keepsDashedContentAndDropsHeaders', () =>
  expect(
    contentLines(
      ['diff --git a/x.sql b/x.sql', '--- a/x.sql', '+++ b/x.sql', '@@ -1 +1 @@', '--- old note', '+++ new note', '-a', '+b'].join('\n'),
    ),
  ).toEqual({ changed: ['-- old note', '++ new note', 'a', 'b'], removed: ['-- old note', 'a'] }))

test('splitNul_keepsNonAsciiPathsUnquoted', () =>
  expect(splitNul('backend/src/Auth/Quản.cs\0docs/a.md\0')).toEqual(['backend/src/Auth/Quản.cs', 'docs/a.md']))

test.each<[string, Tier]>([
  ['backend/tests/StarterKit.API.Tests/Architecture/AnonymousAccessTests.cs', 1],
  ['backend/tests/StarterKit.API.Tests/Architecture/ArchitectureTests.cs', 1],
  ['backend/templates/starterkit-entity/src/StarterKit.Application/Services/Samples/SampleService.cs', 2],
  ['backend/templates/starterkit-entity/src/StarterKit.API/Controllers/SamplesController.cs', 2],
  ['backend/templates/starterkit-entity/.template.config/template.json', 2],
])('tierOf(%s) = %d (guards and templates that multiply into every slice)', (path, tier) =>
  expect(tierOf(path)).toBe(tier))
