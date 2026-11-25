import { expect, test } from 'bun:test'
import { checkReleasePlans, type ReleasePlanInput } from './check-release-plan'

const APPROVED_LOG = [
  '| 2026-11-25 | 1.0 | BA review | BA review |',
  '| 2026-11-25 | 1.0 | Người dùng duyệt | Quyết định của người dùng |',
]

const header = (status: string, extra: string[]) => [
  '| Mục | Giá trị |',
  '|---|---|',
  ...extra,
  `| Trạng thái | ${status} |`,
  '| Phiên bản | 1.0 |',
  '',
]

const PRD_ROWS = [
  '| PRD01-US-01 | P-01 | trưởng nhóm | xem | biết | Must |',
  '| PRD01-US-02 | P-01 | trưởng nhóm | sửa | đúng | Must |',
  '| PRD01-US-03 | P-01 | trưởng nhóm | xuất | gửi | Could |',
  '| PRD01-FR-01 | Hệ thống phải xem | US-01 | Must |',
  '| PRD01-FR-02 | Hệ thống phải sửa | US-02 | Must |',
  '| PRD01-FR-03 | Hệ thống phải xuất | US-03 | Could |',
  '| PRD01-AC-01 | FR-01 | a | b | c |',
  '| PRD01-AC-02 | FR-01 | a | b | c |',
  '| PRD01-AC-03 | FR-02 | a | b | c |',
  '| PRD01-AC-04 | FR-03 | a | b | c |',
]

const prd = (status: string, rows: string[] = PRD_ROWS) =>
  ['# PRD-01 — Thử', '', ...header(status, ['| Mã | `PRD01` (tiền tố) |']), ...rows, ''].join('\n')

const rel = (n: number, stories: string, size = 'M') => `| PRD01-REL-${n} | Xem được | ${stories} | P-01 | — | ${size} |`

const releasePlan = (status: string, rels: string[], changeLog: string[] = APPROVED_LOG, code = 'PRD01') =>
  [
    `# ${code} — Kế hoạch phát hành`,
    '',
    ...header(status, [`| PRD | \`${code}\` v1.0 |`]),
    '| REL | Mục tiêu | Story | Persona | Phụ thuộc | Cỡ áo |',
    '|---|---|---|---|---|---|',
    ...rels,
    '',
    '| Ngày | Phiên bản | Thay đổi | Nguồn |',
    '|---|---|---|---|',
    ...changeLog,
    '',
  ].join('\n')

const plan = (...text: string[]) => ['# Kế hoạch', '', ...text, ''].join('\n')

const FULL = [rel(1, 'PRD01-US-01; PRD01-US-02')]
const LINE = '**Release plan:** `docs/backlog/PRD01-x.md`'
const SPRINT = '2026-11-25-x-SPR-2026-12-S1.md'

const check = (input: Partial<ReleasePlanInput>) =>
  checkReleasePlans({ prds: { '01-x.md': prd('Approved') }, backlog: {}, plans: {}, ...input })

const checkRels = (rels: string[], status = 'Approved') => check({ backlog: { 'PRD01-x.md': releasePlan(status, rels) } })

const checkPlan = (text: string, status = 'Approved', name = 'p.md') =>
  check({ backlog: { 'PRD01-x.md': releasePlan(status, FULL) }, plans: { [name]: text } })

const inBacklog = (...messages: string[]) => messages.map((m) => `PRD01-x.md: ${m}`)

test('emptyInput_passes', () => expect(checkReleasePlans({ prds: {}, backlog: {}, plans: {} })).toEqual([]))

test('coveredApprovedPlan_passes', () => expect(checkRels(FULL)).toEqual([]))

test('storyCellVariants_normalise', () =>
  expect(
    checkRels([rel(1, ' PRD01-US-01 (PRD01-AC-01) '), rel(2, ' PRD01-US-01 (AC-02);PRD01-US-02 ;')]),
  ).toEqual([]))

test('unknownPrd_fails', () =>
  expect(check({ backlog: { 'PRD09-x.md': releasePlan('Draft', [], [], 'PRD09') } })).toEqual([
    'PRD09-x.md: PRD PRD09 not found',
  ]))

test('foreignRelPrefix_fails', () =>
  expect(checkRels([...FULL, '| PRD02-REL-1 | x | PRD01-US-03 | P-01 | — | M |'])).toEqual(
    inBacklog('PRD02-REL-1 does not belong to PRD01'),
  ))

test('foreignStoryPrefix_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01; PRD01-US-02; PRD02-US-01')])).toEqual(
    inBacklog('PRD01-REL-1 story PRD02-US-01 does not belong to PRD01'),
  ))

test('unknownStory_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01; PRD01-US-02; PRD01-US-09')])).toEqual(
    inBacklog('PRD01-REL-1 unknown story PRD01-US-09'),
  ))

test('unreadableStoryEntry_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01; PRD01-US-02; US-03')])).toEqual(inBacklog('PRD01-REL-1 unknown story US-03')))

test('foreignAc_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-02 (AC-01)')], 'Draft')).toEqual(
    inBacklog('PRD01-REL-1 PRD01-US-02 lists PRD01-AC-01, which is not an AC of that story'),
  ))

test('badSize_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01; PRD01-US-02', 'XXL')])).toEqual(
    inBacklog('PRD01-REL-1 size must be S, M, L or XL: XXL'),
  ))

test('duplicateRel_fails', () =>
  expect(checkRels([...FULL, rel(1, 'PRD01-US-03')])).toEqual(inBacklog('duplicate PRD01-REL-1')))

test('wholeTwice_fails', () =>
  expect(checkRels([...FULL, rel(2, 'PRD01-US-01')])).toEqual(
    inBacklog('PRD01-US-01 is planned whole in PRD01-REL-1 and again in PRD01-REL-2'),
  ))

test('partialThenWhole_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01 (AC-01)'), rel(2, 'PRD01-US-01; PRD01-US-02')])).toEqual(
    inBacklog('PRD01-US-01 is planned whole in PRD01-REL-2 and again in PRD01-REL-1'),
  ))

test('overlappingAc_fails', () =>
  expect(
    checkRels([rel(1, 'PRD01-US-01 (AC-01, AC-02); PRD01-US-02'), rel(2, 'PRD01-US-01 (AC-02)')]),
  ).toEqual(inBacklog('PRD01-US-01 PRD01-AC-02 is in both PRD01-REL-1 and PRD01-REL-2')))

test('mustStoryMissing_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01')])).toEqual(inBacklog('Must story PRD01-US-02 is in no REL')))

test('splitMustStoryUncovered_fails', () =>
  expect(checkRels([rel(1, 'PRD01-US-01 (AC-01); PRD01-US-02')])).toEqual(
    inBacklog('Must story PRD01-US-01 is split but PRD01-AC-02 are in no REL'),
  ))

test('couldStoryMissing_passes', () => expect(checkRels(FULL)).toEqual([]))

test('draftReleasePlan_skipsCoverage', () => expect(checkRels([rel(1, 'PRD01-US-01')], 'Draft')).toEqual([]))

test('prdInReview_skipsCoverage', () =>
  expect(
    check({
      prds: { '01-x.md': prd('In review') },
      backlog: { 'PRD01-x.md': releasePlan('Approved', [rel(1, 'PRD01-US-01')]) },
    }),
  ).toEqual([]))

test('approvedWithoutUserApprovalRow_fails', () =>
  expect(check({ backlog: { 'PRD01-x.md': releasePlan('Approved', FULL, [APPROVED_LOG[0]]) } })).toEqual(
    inBacklog('no user-approval row for v1.0'),
  ))

test('readmeAndTemplate_areIgnored', () =>
  expect(check({ backlog: { 'README.md': 'x', '_template.md': releasePlan('Draft', [], [], 'PRD09') } })).toEqual([]))

test('planWithoutPrdIds_isIgnored', () => expect(checkPlan(plan('Sửa layout.'))).toEqual([]))

test('idsOnlyInCode_areIgnored', () =>
  expect(
    checkPlan(
      plan(
        'Ví dụ `PRD01-US-01` và ``PRD01-REL-9``.',
        '````md',
        '```',
        'PRD01-AC-01',
        '```',
        'PRD01-FR-01',
        '````',
        '  ~~~',
        '  PRD01-US-02',
        '  ~~~',
      ),
    ),
  ).toEqual([]))

test('idAfterClosedFence_isDetected', () =>
  expect(checkPlan(plan('```', 'x', '```', 'Làm PRD01-US-01.'))).toEqual([
    'p.md: mentions PRD ids but has no **Release plan:** line',
  ]))

test('lineStartingWithTripleBacktickSpan_doesNotOpenAFence', () =>
  expect(checkPlan(plan('```x``` là ví dụ.', 'Làm PRD01-US-01.'))).toEqual([
    'p.md: mentions PRD ids but has no **Release plan:** line',
  ]))

test('crlfFence_isIgnored', () => expect(checkPlan('```\r\nPRD01-US-01\r\n```\r\n')).toEqual([]))

test('planWithoutReleasePlanLine_fails', () =>
  expect(checkPlan(plan('Làm PRD01-US-01.', `- ${LINE}`))).toEqual([
    'p.md: mentions PRD ids but has no **Release plan:** line',
  ]))

test('sprintNamedPlanWithoutLine_fails', () =>
  expect(checkPlan(plan('Mục tiêu sprint.'), 'Approved', SPRINT)).toEqual([
    `${SPRINT}: sprint plan has no **Release plan:** line`,
  ]))

test('planWithMissingReleasePlan_fails', () =>
  expect(checkPlan(plan('**Release plan:** docs/backlog/PRD02-y.md'))).toEqual([
    'p.md: release plan docs/backlog/PRD02-y.md not found',
  ]))

test('planWithDraftReleasePlan_fails', () =>
  expect(checkPlan(plan(LINE), 'Draft')).toEqual(['p.md: release plan docs/backlog/PRD01-x.md is not Approved']))

test('planWithUnapprovedPrd_fails', () =>
  expect(
    check({
      prds: { '01-x.md': prd('In review') },
      backlog: { 'PRD01-x.md': releasePlan('Approved', FULL) },
      plans: { 'p.md': plan(LINE) },
    }),
  ).toEqual(['p.md: PRD PRD01 is not Approved']))

test('planWithUnknownRel_fails', () =>
  expect(checkPlan(plan(LINE, 'Làm `PRD01-REL-9`, PRD01-REL-9 và PRD01-REL-1.'))).toEqual([
    'p.md: unknown PRD01-REL-9',
  ]))

test('validSprintPlan_passes', () =>
  expect(checkPlan(plan(LINE, 'PRD01-REL-1: PRD01-US-01 (AC-01).'), 'Approved', SPRINT)).toEqual([]))
