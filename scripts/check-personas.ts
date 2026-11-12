// Persona gate (docs/process/lifecycle.md § Persona): once any PRD besides the persona file exists, the persona
// file must be Approved, and every user story names only personas it defines (`### P-nn — …` headings).
// Usage: bun scripts/check-personas.ts [prd-dir]   (default docs/prd; exit 1 on any finding)

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { cells, plain, status } from './check-prd-approval'

export const PERSONA_FILE = '00-personas.md'

const STORY_ID = /^PRD\d+-US-\d+$/
const PERSONA_ID = /^P-\d{2}$/
const PERSONA_HEADING = /^###\s+(P-\d{2})\b/gm
const GATE = 'personas must be Approved before any PRD (lifecycle.md § Persona)'

const isPrd = (name: string) => name.endsWith('.md') && name !== 'README.md' && !name.startsWith('_')

function checkStories(name: string, markdown: string, defined: Set<string>): string[] {
  const findings: string[] = []
  for (const line of markdown.split('\n').filter((l) => l.startsWith('|'))) {
    const [id, persona = ''] = cells(line).map(plain)
    if (!STORY_ID.test(id)) continue
    const codes = persona.split(',').map((p) => p.trim())
    if (!codes.every((p) => PERSONA_ID.test(p))) {
      findings.push(`${name}: ${id} Persona column must hold P-nn codes: ${persona}`)
      continue
    }
    for (const p of codes.filter((p) => !defined.has(p))) findings.push(`${name}: ${id} names unknown persona ${p}`)
  }
  return findings
}

export function checkPersonaGate(files: Record<string, string>): string[] {
  const prds = Object.keys(files).filter((n) => isPrd(n) && n !== PERSONA_FILE)
  if (prds.length === 0) return []

  const personas = files[PERSONA_FILE]
  if (personas === undefined) return [`${PERSONA_FILE} is missing: ${GATE}`]
  if (status(personas) !== 'Approved') return [`${PERSONA_FILE} is not Approved: ${GATE}`]

  const defined = new Set([...personas.matchAll(PERSONA_HEADING)].map((m) => m[1]))
  return prds.flatMap((n) => checkStories(n, files[n], defined))
}

if (import.meta.main) {
  const dir = process.argv[2] ?? 'docs/prd'
  const files: Record<string, string> = {}
  if (existsSync(dir)) {
    for (const name of readdirSync(dir).filter(isPrd)) files[name] = readFileSync(join(dir, name), 'utf8')
  }
  const findings = checkPersonaGate(files)
  for (const finding of findings) console.log(finding)
  process.exit(findings.length > 0 ? 1 : 0)
}
