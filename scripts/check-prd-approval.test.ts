import { expect, test } from 'bun:test'
import { checkPrdApproval } from './check-prd-approval'

const prd = (status: string, version: string, rows: string[]) =>
  [
    '# PRD-99 — Thử',
    '',
    '| Mục | Giá trị |',
    '|---|---|',
    `| Trạng thái | ${status} |`,
    `| Phiên bản | ${version} |`,
    '',
    '## 14. Lịch sử thay đổi',
    '',
    '| Ngày | Phiên bản | Thay đổi | Nguồn |',
    '|---|---|---|---|',
    ...rows,
    '',
  ].join('\n')

const draft = '| 2026-10-20 | 1.0 | Bản nháp | — |'
const ba = (v: string) => `| 2026-10-21 | ${v} | Sửa theo review | BA review |`
const approve = (v: string) => `| 2026-10-22 | ${v} | Người dùng duyệt PRD | Quyết định của người dùng |`

test('approvedWithBaReviewThenApproval_passes', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0')]))).toEqual([]))

test('approvedWithoutBaReview_fails', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, approve('1.0')]))).toEqual([
    'no BA review before the approval of v1.0',
  ]))

test('contentAfterApproval_fails', () =>
  expect(
    checkPrdApproval(
      prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | Thêm AC-9 | SA (thay người dùng) |']),
    ),
  ).toEqual(['content change after the approval of v1.0: Thêm AC-9']))

test('metadataAfterApproval_passes', () =>
  expect(
    checkPrdApproval(
      prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | Điền dòng Kế hoạch | metadata |']),
    ),
  ).toEqual([]))

test('notApproved_isSkipped', () => expect(checkPrdApproval(prd('In review', '1.0', [draft]))).toEqual([]))

test('approvedWithoutApprovalRow_fails', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0')]))).toEqual(['no user-approval row for v1.0']))

test('onlyRowsOfTheCurrentVersionCount', () =>
  expect(checkPrdApproval(prd('Approved', '1.1', [draft, ba('1.0'), approve('1.0'), approve('1.1')]))).toEqual([
    'no BA review before the approval of v1.1',
  ]))

test('lastApprovalRowIsTheOneChecked', () =>
  expect(
    checkPrdApproval(
      prd('Approved', '1.1', [approve('1.1'), ba('1.1'), '| 2026-10-23 | 1.1 | Người dùng duyệt lại | Quyết định của người dùng |']),
    ),
  ).toEqual([]))

// Final review: inputs that must not pass silently.
const withHeader = (statusCell: string, rows: string[]) =>
  prd('Approved', '1.0', rows).replace('| Trạng thái | Approved |', statusCell)

test.each(['| Trạng thái | **Approved** |', '| Trạng thái | *Approved* |', '| Trạng thái | Approved (2026-10-22) |', '| Trạng thái | Approved | ghi chú |'])(
  'decoratedApprovedStatus(%s)_isStillChecked',
  (cell) => expect(checkPrdApproval(withHeader(cell, [draft, approve('1.0')]))).toEqual(['no BA review before the approval of v1.0']),
)

test('missingStatus_fails', () =>
  expect(checkPrdApproval(withHeader('| Mã | PRD99 |', [draft]))).toEqual(['no recognised Trạng thái (Draft, In review, Approved)']))

test('unknownStatus_fails', () =>
  expect(checkPrdApproval(withHeader('| Trạng thái | Xong |', [draft]))).toEqual(['no recognised Trạng thái (Draft, In review, Approved)']))

test('rowNewerThanHeaderVersion_fails', () =>
  expect(
    checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.1 | Thêm FR mới | Quyết định của người dùng |'])),
  ).toEqual(['row for v1.1 is newer than the header version v1.0']))

test('negatedBaReview_doesNotCount', () =>
  expect(
    checkPrdApproval(prd('Approved', '1.0', [draft, '| 2026-10-21 | 1.0 | x | Không có BA review |', approve('1.0')])),
  ).toEqual(['no BA review before the approval of v1.0']))

test('escapedPipeCannotForgeMetadata', () =>
  expect(
    checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | Thêm BR-99 \\| metadata | Quyết định của người dùng |'])),
  ).toEqual(['content change after the approval of v1.0: Thêm BR-99 \\| metadata']))

test('escapedPipeInChangeText_keepsSourceColumn', () =>
  expect(
    checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | sửa `a \\| b` | metadata |'])),
  ).toEqual([]))

test('rowWithWrongCellCount_fails', () =>
  expect(checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 | x |']))).toEqual([
    'change-log row is not | date | version | change | source |: | 2026-10-23 | 1.0 | x |',
  ]))

test('versionCellWithExtraText_fails', () =>
  expect(
    checkPrdApproval(prd('Approved', '1.0', [draft, ba('1.0'), approve('1.0'), '| 2026-10-23 | 1.0 (sửa) | Thêm AC | SA |'])),
  ).toEqual(['change-log version is not N.N: 1.0 (sửa)']))

test('baReviewAsOneOfSeveralSources_counts', () =>
  expect(
    checkPrdApproval(prd('Approved', '1.0', [draft, '| 2026-10-21 | 1.0 | Sửa | SA (thay người dùng); BA review |', approve('1.0')])),
  ).toEqual([]))
