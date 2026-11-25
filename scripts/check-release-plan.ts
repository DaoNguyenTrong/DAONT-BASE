// Release plan gate (docs/process/lifecycle.md § Kế hoạch phát hành): release plans in docs/backlog/ must match
// their PRD, and a sprint plan needs an Approved release plan and PRD.
// Usage: bun scripts/check-release-plan.ts [root]   (default .; exit 1 on any finding)

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { cells, checkPrdApproval, headerValue, plain, status } from './check-prd-approval'

type Files = Record<string, string>
export type ReleasePlanInput = { prds: Files; backlog: Files; plans: Files }

type Story = { must: boolean; acs: string[] }
type Prd = { approved: boolean; stories: Map<string, Story>; acs: Set<string> }
type Entry = { rel: string; story: string; acs: string[] | undefined }

const BACKLOG_DIR = 'docs/backlog/'
const SIZES = ['S', 'M', 'L', 'XL']
const REL_ID = /^PRD\d+-REL-\d+$/
const STORY_ENTRY = /^(PRD\d+-US-\d+)(?:\s*\(([^)]+)\))?$/
const SPRINT_NAME = /SPR-\d{4}-\d{2}-S[12]/
const PLAN_ID = /PRD\d+-(US|FR|AC|REL)-\d+/g
// A short `US-01` or `AC-01` belongs to the release plan's own PRD.
const STORY_OR_AC_ID = /(?<![\w-])(?:(PRD\d+)-)?((?:US|AC)-\d+)/g
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
  return { approved: status(markdown) === 'Approved', stories, acs: new Set(of('AC').map((c) => c[0])) }
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

// Ids already named by another finding are not reported twice.
function checkUnknownIds(markdown: string, code: string, prd: Prd, findings: string[]): string[] {
  const named = [...markdown.matchAll(STORY_OR_AC_ID)].map(([, own = code, id]) => `${own}-${id}`)
  return [...new Set(named)]
    .filter((id) => !prd.stories.has(id) && !prd.acs.has(id))
    .filter((id) => !findings.some((f) => new RegExp(`${id}(?!\\d)`).test(f)))
    .map((id) => `${id} is not in ${code}`)
}

function checkBacklogFile(markdown: string, prds: Files): string[] {
  const approved = status(markdown) === 'Approved'
  const findings = checkPrdApproval(markdown)
  const code = prdCode(headerValue(markdown, 'PRD'))
  const prd = code && findPrd(prds, code)
  if (!code || !prd) return [...findings, `PRD ${code ?? '(none in the header PRD row)'} not found`]

  const table = rows(markdown).filter((c) => REL_ID.test(c[0]))
  const entries = parseEntries(table, code, prd, findings)
  findings.push(...checkOverlaps(entries))
  findings.push(...checkUnknownIds(markdown, code, prd, findings))
  if (approved && prd.approved) findings.push(...checkCoverage(prd, entries))
  return findings
}

function unfenced(markdown: string): string {
  let fence: string | undefined
  const kept: string[] = []
  for (const line of markdown.split('\n')) {
    const [, marker, rest = ''] = FENCE.exec(line.trimEnd()) ?? []
    if (fence) {
      if (marker?.startsWith(fence) && rest.trim() === '') fence = undefined
    } else if (marker && !(marker.startsWith('`') && rest.includes('`'))) fence = marker
    else kept.push(line)
  }
  return kept.join('\n')
}

const withoutCodeSpans = (markdown: string) => markdown.replace(/(`+).*?\1/g, '')

const backlogCode = (markdown: string) => prdCode(headerValue(markdown, 'PRD'))

function checkPlanFile(name: string, markdown: string, { prds, backlog }: ReleasePlanInput): string[] {
  const text = unfenced(markdown)
  const line = text.split('\n').find((l) => l.startsWith(PLAN_LINE_KEY))
  const path = line?.slice(PLAN_LINE_KEY.length).match(/^\s*`?([^`\s]+)/)?.[1]

  if (path === undefined) {
    // Without the line, an id quoted as inline code does not make a file a sprint plan.
    if (withoutCodeSpans(text).match(PLAN_ID)) return [`mentions PRD ids but has no ${PLAN_LINE_KEY} line`]
    const isSprint = SPRINT_NAME.test(name) || line !== undefined
    return isSprint ? [`sprint plan has no ${PLAN_LINE_KEY} line`] : []
  }

  const linked = path.startsWith(BACKLOG_DIR) ? path.slice(BACKLOG_DIR.length) : ''
  if (!isDoc(linked) || !Object.hasOwn(backlog, linked)) return [`release plan ${path} not found`]

  const planIds: string[] = text.match(PLAN_ID) ?? []
  const linkedCode = backlogCode(backlog[linked])
  const codes = new Set([linkedCode, ...planIds.map(prdCode)])
  const findings: string[] = []
  const knownRels = new Set<string>()
  for (const code of codes) {
    const file =
      code === linkedCode
        ? linked
        : Object.keys(backlog).find((n) => isDoc(n) && backlogCode(backlog[n]) === code)
    if (file === undefined) {
      findings.push(`no release plan for ${code}`)
      continue
    }
    if (status(backlog[file]) !== 'Approved') findings.push(`release plan ${BACKLOG_DIR}${file} is not Approved`)
    if (code && !findPrd(prds, code)?.approved) findings.push(`PRD ${code} is not Approved`)
    for (const cell of rows(backlog[file])) knownRels.add(cell[0])
  }

  // A module with no release plan is one finding, not one more per REL.
  const unknown = planIds.filter(
    (id) => id.includes('-REL-') && !knownRels.has(id) && !findings.includes(`no release plan for ${prdCode(id)}`),
  )
  return [...findings, ...[...new Set(unknown)].map((rel) => `unknown ${rel}`)]
}

function checkDuplicates(backlog: Files): string[] {
  const findings: string[] = []
  const owners = new Map<string, string>()
  for (const [name, markdown] of Object.entries(backlog).filter(([n]) => isDoc(n))) {
    const code = backlogCode(markdown)
    if (!code) continue
    const prefix = name.match(/^(PRD\d+)-/)?.[1]
    if (prefix && prefix !== code) findings.push(`${name}: file name prefix ${prefix} differs from header PRD ${code}`)
    const owner = owners.get(code)
    if (owner) findings.push(`${name}: ${code} already has a release plan: ${owner}`)
    else owners.set(code, name)
  }
  return findings
}

export function checkReleasePlans(input: ReleasePlanInput): string[] {
  const prefixed = (name: string, findings: string[]) => findings.map((f) => `${name}: ${f}`)
  return [
    ...checkDuplicates(input.backlog),
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
