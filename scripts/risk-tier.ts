// Computes a change's risk tier; the rule each tier triggers is docs/process/lifecycle.md § Hạng kiểm tra.
// Usage: bun scripts/risk-tier.ts [--base <ref>] [--head <ref>]   (default origin/dev...HEAD)
//        bun scripts/risk-tier.ts --fix-ratio <range>             (retro: fix commits per tier)

import { spawnSync } from 'node:child_process'

export type Tier = 0 | 1 | 2
// removedLines is optional so callers that only know the changed lines still get a path-and-content tier.
export type ChangedFile = { path: string; changedLines: string[]; removedLines?: string[] }

const SENSITIVE_WORDS = [
  'Auth', 'Token', 'Cookie', 'Session', 'Permission', 'Tenant', 'ApiKey', 'Lockout', 'Role', 'Organization',
  'Invitation', 'Member', 'Csrf', 'Jwt', 'Cors', 'RateLimit', 'Security', 'Password', 'Secret', 'CurrentUser',
  'ForwardedHeaders', 'Realtime', 'Anonymous',
].join('|')
// Matched against the file name only: every backend path contains "StarterKit".
const SENSITIVE_FILE_NAME = new RegExp(`(${SENSITIVE_WORDS})[^/]*$|Hub\\.cs$`, 'i')

export const TIER2_PATHS: RegExp[] = [
  /^\.github\//i,
  /^lefthook\.ya?ml$/i,
  /^scripts\/(risk-tier(\.test)?\.ts|test-gate\.sh|claude-bash-guard\.ts)$/i,
  /^\.claude\/settings(\.local)?\.json$/i,
  /^\.codex\/(hooks\.json|config\.toml)$/i,
  /(^|\/)(docker-compose[^/]*\.ya?ml|Dockerfile[^/]*|\.env[^/]*|appsettings[^/]*\.json|launchSettings\.json)$/i,
  /(^|\/)(package\.json|bun\.lockb?|Directory\.(Build|Packages)\.props)$|\.csproj$/i,
  /\/Migrations\//i,
  /^backend\/.*\/(Auth|Authorization|ApiKeys|Organizations|Roles|Invitations|Members|Middleware|Security)\//i,
  /^backend\/templates\/[^/]+\/(src|\.template\.config)\//i,
  /^backend\/src\/StarterKit\.API\/Program\.cs$/i,
  /^backend\/src\/StarterKit\.Infrastructure\/Persistence\/AppDbContext\.cs$/i,
  /^frontend\/src\/api\/(client|mutator)\.ts$/i,
  /^frontend\/src\/router\//i,
]

const SOURCE_FILE = /^(backend|frontend)\/src\/.*\.(cs|ts|vue)$/i

export const TIER2_CONTENT: RegExp[] = [
  /\[(AllowAnonymous|Authorize)\b/,
  /HasQueryFilter|IgnoreQueryFilters|ExecuteSqlRaw|FromSqlRaw/,
  /(Add|Use)(Authentication|Authorization)|RequireAuthorization|RequireRole|IsInRole|AddJwtBearer|AddCors/,
  /\bSameSite\b|\bHttpOnly\b|\bSecure\s*=/,
  /withCredentials|document\.cookie|credentials:\s*['"]include|(local|session)Storage\.setItem\([^)]*token/i,
]

export const TIER0_PATHS: RegExp[] = [
  /^docs\//i,
  /\.md$/i,
  /^shared\/openapi\//i,
  /^frontend\/src\/api\/generated\//i,
  /^frontend\/src\/locales\//i,
  /^\.claude\//i,
  /^\.codex\//i,
  /^\.agents\//i,
]

export const TEST_PATHS: RegExp[] = [/^backend\/tests\//i, /^frontend\/tests\//i, /\.test\.ts$/i]

// Tests that guard every endpoint: loosening one weakens a tier-2 control, so they are never tier 0.
const ARCHITECTURE_TESTS = /\/Architecture\//i

const CONTENT_CHECKED = /\.(cs|ts|vue)$/i

const matches = (patterns: RegExp[], text: string) => patterns.some((p) => p.test(text))

const isSensitivePath = (path: string) =>
  matches(TIER2_PATHS, path) || (SOURCE_FILE.test(path) && SENSITIVE_FILE_NAME.test(path))

export function tierOf(path: string): Tier {
  // A test under a sensitive path stays tier 1: deleting its assertions weakens a tier-2 guard.
  if (matches(TEST_PATHS, path)) {
    return matches(TIER2_PATHS, path) || ARCHITECTURE_TESTS.test(path) || new RegExp(`(${SENSITIVE_WORDS})`, 'i').test(path) ? 1 : 0
  }
  if (isSensitivePath(path)) return 2
  if (matches(TIER0_PATHS, path)) return 0
  return 1
}

function fileTier({ path, changedLines, removedLines }: ChangedFile): { tier: Tier; why: string } {
  const tier = tierOf(path)
  if (tier === 2) return { tier, why: 'sensitive path' }
  const isTest = matches(TEST_PATHS, path)
  if (tier === 1 && !isTest && CONTENT_CHECKED.test(path)) {
    const line = changedLines.find((l) => matches(TIER2_CONTENT, l))
    if (line !== undefined) return { tier: 2, why: `changes "${line.trim()}"` }
  }
  if (tier === 0 && isTest && (removedLines?.length ?? 0) > 0) return { tier: 1, why: 'removes test lines' }
  return { tier, why: tier === 0 ? 'docs, generated or added tests' : 'ordinary code' }
}

export function tierOfChange(files: ChangedFile[]): { tier: Tier; reasons: string[] } {
  const rated = files.map((f) => ({ path: f.path, ...fileTier(f) }))
  const tier = rated.reduce<Tier>((max, r) => (r.tier > max ? r.tier : max), 0)
  const reasons = rated
    .filter((r) => r.tier === tier)
    .slice(0, 5)
    .map((r) => `${r.path}: ${r.why}`)
  return { tier, reasons }
}

export type CommitInfo = { subject: string; files: ChangedFile[] }
type Ratio = Record<Tier, { fix: number; total: number }>

const FIX_SUBJECT = /^fix(\(|!|:)/

export function fixRatioByTier(commits: CommitInfo[]): Ratio {
  const ratio: Ratio = { 0: { fix: 0, total: 0 }, 1: { fix: 0, total: 0 }, 2: { fix: 0, total: 0 } }
  for (const c of commits) {
    if (c.files.length === 0) continue
    const row = ratio[tierOfChange(c.files).tier]
    row.total++
    if (FIX_SUBJECT.test(c.subject)) row.fix++
  }
  return ratio
}

export function formatFixRatio(ratio: Ratio): string {
  const rows = ([0, 1, 2] as const).map((t) => {
    const { fix, total } = ratio[t]
    return `| ${t} | ${fix} | ${total} | ${total === 0 ? '—' : `${Math.round((fix / total) * 100)}%`} |`
  })
  return ['| Hạng | fix | Tổng | Tỉ lệ |', '|---|---|---|---|', ...rows].join('\n')
}

// Content lines of a unified diff, without their +/- marker; file headers before the first hunk are skipped.
export function contentLines(diff: string): { changed: string[]; removed: string[] } {
  const changed: string[] = []
  const removed: string[] = []
  let inHunk = false
  for (const line of diff.split('\n')) {
    if (line.startsWith('diff --git ')) inHunk = false
    else if (line.startsWith('@@')) inHunk = true
    else if (inHunk && (line.startsWith('+') || line.startsWith('-'))) {
      changed.push(line.slice(1))
      if (line.startsWith('-')) removed.push(line.slice(1))
    }
  }
  return { changed, removed }
}

export const splitNul = (s: string): string[] => s.split('\0').filter(Boolean)

// Fails closed: an unreadable diff must never print a tier, because tier 0 would drop every check.
function fail(message: string): never {
  process.stderr.write(`risk-tier: ${message}\n`)
  process.exit(2)
}

function git(args: string[]): string {
  const r = spawnSync('git', ['-c', 'core.quotepath=false', ...args], { encoding: 'utf8', maxBuffer: 256 << 20 })
  if (r.status !== 0) fail(`cannot run git ${args.join(' ')}\n${r.stderr ?? ''}`)
  return r.stdout
}

// `what` is ['diff', '<range>'] or ['show', '--format=', '<sha>'] (show also works for a root commit).
function changedFiles(what: string[]): ChangedFile[] {
  return splitNul(git([...what, '--no-renames', '--name-only', '-z'])).map((path) => {
    const { changed, removed } = contentLines(git([...what, '--no-renames', '-U0', '--', path]))
    return { path, changedLines: changed, removedLines: removed }
  })
}

function commitsIn(range: string): CommitInfo[] {
  return git(['log', '--no-merges', '--format=%H%x09%s', range])
    .split('\n')
    .filter(Boolean)
    .map((line) => {
      const [sha, ...subject] = line.split('\t')
      return { subject: subject.join('\t'), files: changedFiles(['show', '--format=', sha]) }
    })
}

function argValue(args: string[], flag: string): string | undefined {
  const i = args.indexOf(flag)
  if (i < 0) return undefined
  const value = args[i + 1]
  if (value === undefined || value.startsWith('--')) fail(`${flag} needs a value`)
  return value
}

if (import.meta.main) {
  const args = process.argv.slice(2)
  const fixRange = argValue(args, '--fix-ratio')
  if (fixRange !== undefined) {
    console.log(formatFixRatio(fixRatioByTier(commitsIn(fixRange))))
    process.exit(0)
  }
  const base = argValue(args, '--base') ?? 'origin/dev'
  const head = argValue(args, '--head') ?? 'HEAD'
  const { tier, reasons } = tierOfChange(changedFiles(['diff', `${base}...${head}`]))
  console.log(`Risk tier: ${tier}`)
  for (const reason of reasons) console.log(`- ${reason}`)
}
