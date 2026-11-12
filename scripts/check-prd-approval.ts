// Checks the approval order of an Approved PRD (docs/prd/README.md § Quy trình, step 4): a BA review row before the
// last user-approval row of the current version, and nothing but `metadata` rows after it.
// Usage: bun scripts/check-prd-approval.ts <prd.md>...   (exit 1 on any finding)

import { readFileSync } from 'node:fs'

type Row = { version: string; change: string; source: string }

const STATUSES = ['Draft', 'In review', 'Approved'] as const
const VERSION = /^\d+\.\d+$/
const DATE_ROW = /^\|\s*\d{4}-\d{2}-\d{2}\s*\|/

// `\|` is a literal pipe inside a cell, never a column break.
export const cells = (line: string) =>
  line
    .trim()
    .replace(/^\|/, '')
    .replace(/(?<!\\)\|$/, '')
    .split(/(?<!\\)\|/)
    .map((c) => c.trim())

export const plain = (cell: string) => cell.replace(/\*/g, '').trim()

function headerValue(markdown: string, key: string): string | undefined {
  for (const line of markdown.split('\n')) {
    if (!line.startsWith('|') || DATE_ROW.test(line)) continue
    const c = cells(line)
    if (c.length >= 2 && plain(c[0]) === key) return plain(c[1])
  }
  return undefined
}

export function status(markdown: string): (typeof STATUSES)[number] | undefined {
  const value = headerValue(markdown, 'Trạng thái') ?? ''
  return STATUSES.find((s) => new RegExp(`^${s}\\b`, 'i').test(value))
}

const sources = (r: Row) => r.source.split(';').map((s) => s.trim())
const hasBaReview = (r: Row) => sources(r).some((s) => /^BA review\b/.test(s))
const isApproval = (r: Row) => r.change.startsWith('Người dùng duyệt') && r.source.includes('Quyết định của người dùng')

function compareVersions(a: string, b: string): number {
  const [aMajor, aMinor] = a.split('.').map(Number)
  const [bMajor, bMinor] = b.split('.').map(Number)
  return aMajor - bMajor || aMinor - bMinor
}

export function checkPrdApproval(markdown: string): string[] {
  const current = status(markdown)
  if (current === undefined) return [`no recognised Trạng thái (${STATUSES.join(', ')})`]
  if (current !== 'Approved') return []

  const version = headerValue(markdown, 'Phiên bản') ?? ''
  if (!VERSION.test(version)) return [`header Phiên bản is not N.N: ${version}`]

  const findings: string[] = []
  const all: Row[] = []
  for (const line of markdown.split('\n').filter((l) => DATE_ROW.test(l))) {
    const c = cells(line)
    if (c.length !== 4) findings.push(`change-log row is not | date | version | change | source |: ${line.trim()}`)
    else if (!VERSION.test(c[1])) findings.push(`change-log version is not N.N: ${c[1]}`)
    else all.push({ version: c[1], change: c[2], source: c[3] })
  }
  for (const r of all.filter((r) => compareVersions(r.version, version) > 0)) {
    findings.push(`row for v${r.version} is newer than the header version v${version}`)
  }
  if (findings.length > 0) return findings

  const rows = all.filter((r) => r.version === version)
  const approvalAt = rows.findLastIndex(isApproval)
  if (approvalAt < 0) return [`no user-approval row for v${version}`]

  if (!rows.slice(0, approvalAt).some(hasBaReview)) {
    findings.push(`no BA review before the approval of v${version}`)
  }
  for (const r of rows.slice(approvalAt + 1)) {
    if (r.source !== 'metadata') {
      findings.push(`content change after the approval of v${version}: ${r.change.slice(0, 60)}`)
    }
  }
  return findings
}

if (import.meta.main) {
  let failed = false
  for (const path of process.argv.slice(2)) {
    for (const finding of checkPrdApproval(readFileSync(path, 'utf8'))) {
      console.log(`${path}: ${finding}`)
      failed = true
    }
  }
  process.exit(failed ? 1 : 0)
}
