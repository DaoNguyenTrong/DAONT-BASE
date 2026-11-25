// Release plan gate (docs/process/lifecycle.md § Kế hoạch phát hành): release plans in docs/backlog/ must match
// their PRD, and a sprint plan needs an Approved release plan and PRD.
// Usage: bun scripts/check-release-plan.ts [root]   (default .; exit 1 on any finding)

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { cells, checkPrdApproval, headerValue, plain, status } from './check-prd-approval'

type Files = Record<string, string>
export type ReleasePlanInput = { prds: Files; backlog: Files; plans: Files }

type Story = { must: boolean; acs: string[] }
type Prd = { approved: boolean; stories: Map<string, Story> }
type Entry = { rel: string; story: string; acs: string[] | undefined }

const BACKLOG_DIR = 'docs/backlog/'
const SIZES = ['S', 'M', 'L', 'XL']
const REL_ID = /^PRD\d+-REL-\d+$/
const STORY_ENTRY = /^(PRD\d+-US-\d+)(?:\s*\(([^)]+)\))?$/
const SPRINT_NAME = /SPR-\d{4}-\d{2}-S[12]/
const PLAN_ID = /PRD\d+-(US|FR|AC|REL)-\d+/
const PLAN_LINE_KEY = '**Release plan:**'
const FENCE = /^\s*(`{3,}|~{3,})(.*)$/

const isDoc = (name: string) => name.endsWith('.md') && name !== 'README.md' && !name.startsWith('_')
const prdCode = (value: string | undefined) => value?.match(/PRD\d+/)?.[0]

const rows = (markdown: string) =>
  markdown
    .split('\n')
    .filter((l) => l.startsWith('|'))
    .map((l) => cells(l).map((c) => plain(c).replace(/`/g, '')))

// `US-01` and `PRD01-US-01` both become `PRD01-US-01`.
const ids = (cell: string, kind: 'US' | 'FR', code: string) =>
  [...cell.matchAll(new RegExp(`${kind}-\\d+`, 'g'))].map((m) => `${code}-${m[0]}`)

function parsePrd(markdown: string, code: string): Prd {
  const table = rows(markdown)
  const of = (kind: string) => table.filter((c) => new RegExp(`^${code}-${kind}-\\d+$`).test(c[0]))
  const frStories = new Map(of('FR').map((c) => [c[0], ids(c[2] ?? '', 'US', code)]))
  const acStories = of('AC').map((c) => ({
    ac: c[0],
    stories: ids(c[1] ?? '', 'FR', code).flatMap((fr) => frStories.get(fr) ?? []),
  }))
  const stories = new Map(
    of('US').map((c) => [
      c[0],
      { must: c[c.length - 1].startsWith('Must'), acs: acStories.filter((a) => a.stories.includes(c[0])).map((a) => a.ac) },
    ]),
  )
  return { approved: status(markdown) === 'Approved', stories }
}

function findPrd(prds: Files, code: string): Prd | undefined {
  const name = Object.keys(prds).find((n) => isDoc(n) && prdCode(headerValue(prds[n], 'Mã')) === code)
  return name === undefined ? undefined : parsePrd(prds[name], code)
}

function parseEntries(table: string[][], code: string, prd: Prd, findings: string[]): Entry[] {
  const entries: Entry[] = []
  const seen = new Set<string>()
  for (const [rel, , storyCell = '', , , size = ''] of table) {
    if (!rel.startsWith(`${code}-`)) findings.push(`${rel} does not belong to ${code}`)
    if (seen.has(rel)) findings.push(`duplicate ${rel}`)
    seen.add(rel)
    if (!SIZES.includes(size)) findings.push(`${rel} size must be S, M, L or XL: ${size}`)

    for (const text of storyCell.split(';').map((s) => s.trim()).filter(Boolean)) {
      const [, story, acList] = STORY_ENTRY.exec(text) ?? []
      if (story && !story.startsWith(`${code}-`)) {
        findings.push(`${rel} story ${story} does not belong to ${code}`)
        continue
      }
      const known = prd.stories.get(story)
      if (!known) {
        findings.push(`${rel} unknown story ${story ?? text}`)
        continue
      }
      const listed = acList
        ?.split(',')
        .map((a) => a.trim())
        .filter(Boolean)
        .map((a) => (a.startsWith('AC-') ? `${code}-${a}` : a))
      for (const ac of listed?.filter((a) => !known.acs.includes(a)) ?? []) {
        findings.push(`${rel} ${story} lists ${ac}, which is not an AC of that story`)
      }
      entries.push({ rel, story, acs: listed?.filter((a) => known.acs.includes(a)) })
    }
  }
  return entries
}

function checkOverlaps(entries: Entry[]): string[] {
  const findings: string[] = []
  entries.forEach((later, i) => {
    for (const earlier of entries.slice(0, i).filter((e) => e.story === later.story)) {
      if (!earlier.acs || !later.acs) {
        const [whole, other] = earlier.acs ? [later, earlier] : [earlier, later]
        findings.push(`${later.story} is planned whole in ${whole.rel} and again in ${other.rel}`)
      } else if (earlier.rel !== later.rel) {
        for (const ac of later.acs.filter((a) => earlier.acs?.includes(a))) {
          findings.push(`${later.story} ${ac} is in both ${earlier.rel} and ${later.rel}`)
        }
      }
    }
  })
  return findings
}

function checkCoverage(prd: Prd, entries: Entry[]): string[] {
  const findings: string[] = []
  for (const [id, story] of prd.stories) {
    if (!story.must) continue
    const planned = entries.filter((e) => e.story === id)
    if (planned.length === 0) findings.push(`Must story ${id} is in no REL`)
    else if (planned.every((e) => e.acs)) {
      const missing = story.acs.filter((ac) => !planned.some((e) => e.acs?.includes(ac)))
      if (missing.length > 0) findings.push(`Must story ${id} is split but ${missing.join(', ')} are in no REL`)
    }
  }
  return findings
}

function checkBacklogFile(markdown: string, prds: Files): string[] {
  const approved = status(markdown) === 'Approved'
  const findings = approved ? checkPrdApproval(markdown) : []
  const code = prdCode(headerValue(markdown, 'PRD'))
  const prd = code && findPrd(prds, code)
  if (!code || !prd) return [...findings, `PRD ${code ?? '(none in the header PRD row)'} not found`]

  const table = rows(markdown).filter((c) => REL_ID.test(c[0]))
  const entries = parseEntries(table, code, prd, findings)
  findings.push(...checkOverlaps(entries))
  if (approved && prd.approved) findings.push(...checkCoverage(prd, entries))
  return findings
}

// Drops fenced blocks and inline code spans, so ids quoted as examples do not make a file a sprint plan.
function prose(markdown: string): string {
  let fence: string | undefined
  const kept: string[] = []
  for (const line of markdown.split('\n')) {
    const [, marker, rest = ''] = FENCE.exec(line.trimEnd()) ?? []
    if (fence) {
      if (marker?.startsWith(fence) && rest.trim() === '') fence = undefined
    } else if (marker && !(marker.startsWith('`') && rest.includes('`'))) fence = marker
    else kept.push(line.replace(/(`+).*?\1/g, ''))
  }
  return kept.join('\n')
}

function checkPlanFile(name: string, markdown: string, { prds, backlog }: ReleasePlanInput): string[] {
  const line = markdown.split('\n').find((l) => l.startsWith(PLAN_LINE_KEY))
  const path = line?.slice(PLAN_LINE_KEY.length).match(/^\s*`?([^`\s]+)/)?.[1]
  const mentionsIds = PLAN_ID.test(prose(markdown))

  if (path === undefined) {
    if (mentionsIds) return [`mentions PRD ids but has no ${PLAN_LINE_KEY} line`]
    const isSprint = SPRINT_NAME.test(name) || line !== undefined
    return isSprint ? [`sprint plan has no ${PLAN_LINE_KEY} line`] : []
  }

  const file = path.startsWith(BACKLOG_DIR) ? path.slice(BACKLOG_DIR.length) : ''
  if (!isDoc(file) || !Object.hasOwn(backlog, file)) return [`release plan ${path} not found`]

  const releasePlan = backlog[file]
  const findings: string[] = []
  if (status(releasePlan) !== 'Approved') findings.push(`release plan ${path} is not Approved`)
  const code = prdCode(headerValue(releasePlan, 'PRD'))
  if (code && !findPrd(prds, code)?.approved) findings.push(`PRD ${code} is not Approved`)

  const known = new Set(rows(releasePlan).map((c) => c[0]))
  const named = new Set(markdown.match(/PRD\d+-REL-\d+/g))
  for (const rel of [...named].filter((r) => !known.has(r))) findings.push(`unknown ${rel}`)
  return findings
}

export function checkReleasePlans(input: ReleasePlanInput): string[] {
  const prefixed = (name: string, findings: string[]) => findings.map((f) => `${name}: ${f}`)
  return [
    ...Object.entries(input.backlog)
      .filter(([name]) => isDoc(name))
      .flatMap(([name, markdown]) => prefixed(name, checkBacklogFile(markdown, input.prds))),
    ...Object.entries(input.plans).flatMap(([name, markdown]) => prefixed(name, checkPlanFile(name, markdown, input))),
  ]
}

function readDocs(dir: string): Files {
  const files: Files = {}
  if (existsSync(dir)) {
    for (const name of readdirSync(dir).filter(isDoc)) files[name] = readFileSync(join(dir, name), 'utf8')
  }
  return files
}

if (import.meta.main) {
  const root = process.argv[2] ?? '.'
  const findings = checkReleasePlans({
    prds: readDocs(join(root, 'docs/prd')),
    backlog: readDocs(join(root, BACKLOG_DIR)),
    plans: readDocs(join(root, 'docs/superpowers/plans')),
  })
  for (const finding of findings) console.log(finding)
  process.exit(findings.length > 0 ? 1 : 0)
}
