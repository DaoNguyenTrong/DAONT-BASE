export const meta = {
  name: 'localization-sync-audit',
  description: 'Audit vi/en key parity across backend Resx/const messages and frontend locale files',
  whenToUse:
    'Run before a release, or whenever several PRs touched localization independently, to catch vi/en drift between backend Messages.{vi,en}.resx, DomainMessages.cs/ApplicationMessages.cs, and frontend locales/{vi,en}.ts — these are unsynced systems per localization.md, nothing fails CI if they drift.',
  phases: [
    { title: 'Extract', detail: 'pull key lists from backend resx, backend const files, frontend locale files' },
    { title: 'Report', detail: 'diff key sets and write a findings report' },
  ],
}

const RESX_SCHEMA = {
  type: 'object',
  properties: {
    vi: { type: 'array', items: { type: 'string' } },
    en: { type: 'array', items: { type: 'string' } },
  },
  required: ['vi', 'en'],
}

const CONSTS_SCHEMA = {
  type: 'object',
  properties: {
    domain: { type: 'array', items: { type: 'string' } },
    application: { type: 'array', items: { type: 'string' } },
  },
  required: ['domain', 'application'],
}

const FE_SCHEMA = {
  type: 'object',
  properties: {
    vi: { type: 'array', items: { type: 'string' } },
    en: { type: 'array', items: { type: 'string' } },
  },
  required: ['vi', 'en'],
}

function diff(a, b) {
  return a.filter((x) => !b.includes(x))
}

phase('Extract')

const [resx, consts, fe] = await parallel([
  () =>
    agent(
      `Read backend/src/FeedbackHub.Application/Resources/Messages.resx and backend/src/FeedbackHub.Application/Resources/Messages.en.resx. ` +
        `Extract every <data name="..."> entry, EXCLUDING resx boilerplate keys (resmimetype, version, reader, writer, and any "resheader" entries). ` +
        `Return the real message key names found in each file as two separate lists (one per file). Do not guess — only report keys you actually read.`,
      { label: 'backend-resx', phase: 'Extract', effort: 'low', schema: RESX_SCHEMA },
    ),
  () =>
    agent(
      `Read backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs and backend/src/FeedbackHub.Application/Resources/ApplicationMessages.cs. ` +
        `Each file is a static class of "public const string X = nameof(X);" entries — these are message KEYS (not the localized text itself). ` +
        `Extract the constant names from each file as two separate lists (one per file).`,
      { label: 'backend-consts', phase: 'Extract', effort: 'low', schema: CONSTS_SCHEMA },
    ),
  () =>
    agent(
      `Read frontend/src/locales/vi.ts and frontend/src/locales/en.ts. Both export a nested object literal conforming to the same ` +
        `LocaleSchema TS interface (do NOT read naive-ui.ts, it's a third-party locale, not part of this). ` +
        `Walk each nested object and produce the full dotted path for every LEAF string key ` +
        `(e.g. "common.confirm", "errors.requestFailed" — not intermediate object keys like "common" alone). ` +
        `Return the dotted-path list for each file separately.`,
      { label: 'frontend-locales', phase: 'Extract', effort: 'low', schema: FE_SCHEMA },
    ),
])

if (!resx || !consts || !fe) {
  throw new Error('One or more extraction agents failed — aborting before producing a partial/misleading report.')
}

const resxMissingInEn = diff(resx.vi, resx.en)
const resxMissingInVi = diff(resx.en, resx.vi)
const domainKeysMissingResx = consts.domain.filter((k) => !resx.vi.includes(k) || !resx.en.includes(k))
const applicationKeysMissingResx = consts.application.filter((k) => !resx.vi.includes(k) || !resx.en.includes(k))
const feMissingInEn = diff(fe.vi, fe.en)
const feMissingInVi = diff(fe.en, fe.vi)

log(
  `resx vi/en gaps: ${resxMissingInEn.length + resxMissingInVi.length} | ` +
    `const keys without resx entry: ${domainKeysMissingResx.length + applicationKeysMissingResx.length} | ` +
    `frontend vi/en gaps: ${feMissingInEn.length + feMissingInVi.length}`,
)

phase('Report')

const report = await agent(
  `Viết một báo cáo audit (markdown, tiếng Việt) về đồng bộ vi/en trong dự án FEEDBACK-HUB, dựa trên dữ liệu diff đã tính sẵn dưới đây — ` +
    `KHÔNG tự đọc lại file, chỉ dùng đúng dữ liệu này:\n\n` +
    `\`\`\`json\n${JSON.stringify(
      {
        resxMissingInEn,
        resxMissingInVi,
        domainKeysMissingResx,
        applicationKeysMissingResx,
        feMissingInEn,
        feMissingInVi,
      },
      null,
      2,
    )}\n\`\`\`\n\n` +
    `Cấu trúc report:\n` +
    `1. Tóm tắt (tổng số gap theo từng nhóm)\n` +
    `2. Backend — Messages.resx: key thiếu ở vi / thiếu ở en (liệt kê tên key, chỉ backend/src/FeedbackHub.Application/Resources/Messages.resx và Messages.en.resx)\n` +
    `3. Backend — DomainMessages.cs / ApplicationMessages.cs: const nào KHÔNG có entry tương ứng trong resx (nghĩa là exception này sẽ hiển thị key thô thay vì text) — chỉ rõ file nào chứa const đó\n` +
    `4. Frontend — locales/vi.ts vs en.ts: dotted-path nào thiếu ở bên nào\n` +
    `5. Nếu không có gap ở mục nào, ghi rõ "Không phát hiện lệch" thay vì bỏ qua mục đó\n` +
    `Không đề xuất nội dung dịch cụ thể — chỉ liệt kê gap, việc dịch cần người review.`,
  { phase: 'Report' },
)

return {
  counts: {
    resxMissingInEn: resxMissingInEn.length,
    resxMissingInVi: resxMissingInVi.length,
    domainKeysMissingResx: domainKeysMissingResx.length,
    applicationKeysMissingResx: applicationKeysMissingResx.length,
    feMissingInEn: feMissingInEn.length,
    feMissingInVi: feMissingInVi.length,
  },
  resxMissingInEn,
  resxMissingInVi,
  domainKeysMissingResx,
  applicationKeysMissingResx,
  feMissingInEn,
  feMissingInVi,
  report,
}
