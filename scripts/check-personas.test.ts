import { expect, test } from 'bun:test'
import { checkPersonaGate, PERSONA_FILE } from './check-personas'

const personas = (status: string, ids: string[]) =>
  [
    '# PRD-00 — Persona',
    '',
    '| Mục | Giá trị |',
    '|---|---|',
    `| Trạng thái | ${status} |`,
    '| Phiên bản | 1.0 |',
    '',
    ...ids.map((id) => `### ${id} — Lan, trưởng nhóm CSKH\n`),
    '### AP-01 — Khách vãng lai',
    '',
  ].join('\n')

const prd = (rows: string[]) =>
  [
    '# PRD-01 — Thử',
    '',
    '| Mã | Persona | Là… | Tôi muốn… | Để… | Ưu tiên |',
    '|---|---|---|---|---|---|',
    ...rows,
    '',
  ].join('\n')

const story = (id: string, persona: string) => `| ${id} | ${persona} | trưởng nhóm | xem | biết | Must |`
const approved = personas('Approved', ['P-01', 'P-02'])

test('noModulePrd_passesWithoutPersonaFile', () =>
  expect(checkPersonaGate({ 'README.md': '', '_template.md': prd([story('PRDXX-US-01', 'P-01')]) })).toEqual([]))

test('draftPersonaFileAlone_passes', () => expect(checkPersonaGate({ [PERSONA_FILE]: personas('Draft', ['P-01']) })).toEqual([]))

test('modulePrdWithoutPersonaFile_fails', () =>
  expect(checkPersonaGate({ '01-feedback.md': prd([]) })).toEqual([
    `${PERSONA_FILE} is missing: personas must be Approved before any PRD (lifecycle.md § Persona)`,
  ]))

test('modulePrdWithUnapprovedPersonas_fails', () =>
  expect(checkPersonaGate({ [PERSONA_FILE]: personas('In review', ['P-01']), '01-feedback.md': prd([]) })).toEqual([
    `${PERSONA_FILE} is not Approved: personas must be Approved before any PRD (lifecycle.md § Persona)`,
  ]))

test('storiesWithKnownPersonas_pass', () =>
  expect(
    checkPersonaGate({
      [PERSONA_FILE]: approved,
      '01-feedback.md': prd([story('PRD01-US-01', 'P-01'), story('PRD01-US-02', 'P-01, P-02')]),
    }),
  ).toEqual([]))

test('unknownPersona_fails', () =>
  expect(checkPersonaGate({ [PERSONA_FILE]: approved, '01-feedback.md': prd([story('PRD01-US-01', 'P-03')]) })).toEqual([
    '01-feedback.md: PRD01-US-01 names unknown persona P-03',
  ]))

test('antiPersonaIsNotAPersona', () =>
  expect(checkPersonaGate({ [PERSONA_FILE]: approved, '01-feedback.md': prd([story('PRD01-US-01', 'AP-01')]) })).toEqual([
    '01-feedback.md: PRD01-US-01 Persona column must hold P-nn codes: AP-01',
  ]))

test.each(['', 'trưởng nhóm', 'P-1'])('storyWithoutPersonaCode(%s)_fails', (cell) =>
  expect(checkPersonaGate({ [PERSONA_FILE]: approved, '01-feedback.md': prd([story('PRD01-US-01', cell)]) })).toEqual([
    `01-feedback.md: PRD01-US-01 Persona column must hold P-nn codes: ${cell}`,
  ]))

test('removedStory_isSkipped', () =>
  expect(checkPersonaGate({ [PERSONA_FILE]: approved, '01-feedback.md': prd([story('~~PRD01-US-01~~', '')]) })).toEqual([]))

test('boldStoryId_isStillChecked', () =>
  expect(checkPersonaGate({ [PERSONA_FILE]: approved, '01-feedback.md': prd([story('**PRD01-US-01**', 'P-09')]) })).toEqual([
    '01-feedback.md: PRD01-US-01 names unknown persona P-09',
  ]))

test('templatesAreIgnored', () =>
  expect(checkPersonaGate({ '_template.md': prd([story('PRDXX-US-01', '')]), '_template-persona.md': personas('Draft', []) })).toEqual([]))
