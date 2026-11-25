# Agile Planning Process (release plan, sprints, Plane) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add stage 1b (release plan), stage 1c (architecture runway), the sprint loop with DoR/DoD and the Plane mapping to the DAONT-BASE process, enforced by a new `scripts/check-release-plan.ts`.

**Architecture:** One pure checker function (`checkReleasePlans`) parses PRDs, release plans (`docs/backlog/`) and plans (`docs/superpowers/plans/`) as markdown tables. It reuses the table helpers and approval-order check of `scripts/check-prd-approval.ts` and runs in `scripts/test-gate.sh` and both CIs next to `check-personas.ts`. Everything else is process text: templates, `lifecycle.md`, agent prompts, `AGENTS.md`. Each rule is written in one owning file and referenced elsewhere (`lifecycle.md` § 8 *Mỗi quy tắc một nơi*).

**Tech Stack:** Bun 1.3 + TypeScript scripts with `bun:test`, Markdown process docs (Vietnamese), GitHub Actions and GitLab CI.

**Spec:** `docs/superpowers/specs/2026-11-25-agile-planning-design.md` (Vietnamese, Approved 2026-11-25). Section numbers `spec § x` below refer to it, so read it alongside this plan.

## Global Constraints

- **Branch:** `chore/agile-planning-process`. It already holds the spec commit `1c3214d`; this plan is committed on it too.
  - Stage files by explicit path; use conventional commits; never `--no-verify`.
  - One PR into `dev`, opened only on the user's order.
  - The PR touches `AGENTS.md`, `.claude/`, `.github/` and `scripts/test-gate.sh`, so it is tier 2 and the user merges it. `pr-reviewer` runs with `model: opus` and does not merge.
- **Process docs are Vietnamese**, with full diacritics, written in the voice of the existing `docs/process/lifecycle.md`. Script code, identifiers and finding messages are English.
- **Ids:** story `PRDnn-US-nn`, FR `PRDnn-FR-nn`, AC `PRDnn-AC-nn`, release slice `PRDnn-REL-n`.
  - Inside a release plan's *Story* cell, AC may be written short (`AC-07`). It is normalised with the release plan's PRD code.
  - In a PRD, the FR *Story* column (`US-01`) and the AC *Yêu cầu* column (`FR-01`) use short ids. Normalise them the same way.
- **Fixed values from the spec:**
  - T-shirt sizes: `S`, `M`, `L`, `XL`.
  - Story points: Fibonacci `1, 2, 3, 5, 8, 13`; a story above 8 must be split.
  - Velocity is the mean of the last 3 sprints.
  - Sprint cycles are named `SPR-YYYY-MM-S1` (day 1–15) and `SPR-YYYY-MM-S2` (day 16 to end of month).
  - Plane states: `Backlog, Todo, In Progress, Ready For Testing, Testing, Done, Pending, Cancelled`.
- **Release plan files:** `docs/backlog/PRDnn-<slug>.md`. Files named `README.md` or starting with `_` are skipped by the checker, the same as in `docs/prd/`.
- **Approval-order check:** a release plan uses the PRD change-log format `| Ngày | Phiên bản | Thay đổi | Nguồn |` and the same approval-order rule. It is checked by calling `checkPrdApproval`, not by a copy of that logic.
- **Process freeze:** the change applies now, outside a retro, because no module is running. Task 4 records that reason in `docs/process/decisions.md`.

## Review Focus

1. A *Story* cell with extra spaces, a trailing `;`, or AC written full (`PRD01-AC-07`) instead of short must give no false finding. Covered by test `storyCellVariants_normalise` (Task 1).
2. A **Draft** release plan that does not cover every Must story yet must pass, because coverage is checked only when the release plan and its PRD are both Approved. Covered by test `draftReleasePlan_skipsCoverage` (Task 1).
3. A PRD sent back to *In review* for an M change, while its release plan stays Approved, must not turn CI red on coverage. Covered by test `prdInReview_skipsCoverage` (Task 1).
4. Existing plans in `docs/superpowers/plans/` mention no PRD id, so they must stay ignored. Covered by test `planWithoutPrdIds_isIgnored` (Task 1).
5. A missing `docs/backlog/` directory, or a release plan whose PRD code matches no PRD, must give a finding or nothing, never a crash. Covered by tests `emptyInput_passes` and `unknownPrd_fails` (Task 1), plus Task 2 step 2.

---

### Task 1: `check-release-plan.ts` checker

Risk tier 1. Review: none per task. Covered by the phase `pr-reviewer`.

**Files:**
- Modify: `scripts/check-prd-approval.ts`. Export the existing `headerValue`, with no behaviour change.
- Create: `scripts/check-release-plan.ts`
- Test: `scripts/check-release-plan.test.ts`

**Interfaces:**
- Consumes: from `./check-prd-approval`, `cells(line)`, `plain(cell)`, `status(markdown)`, `headerValue(markdown, key)` (newly exported) and `checkPrdApproval(markdown): string[]`.
- Produces:
  - `export type ReleasePlanInput = { prds: Record<string, string>; backlog: Record<string, string>; plans: Record<string, string> }`. Each key is a file name inside `docs/prd/`, `docs/backlog/` or `docs/superpowers/plans/`; each value is that file's markdown.
  - `export function checkReleasePlans(input: ReleasePlanInput): string[]`
  - CLI: `bun scripts/check-release-plan.ts [root]` (default `.`). It reads the three directories and skips any that is missing. It prints one finding per line and exits 1 if there is any finding.

**Parsing rules:**
- **PRD of a release plan:** the first `PRD\d+` token in the release plan's header row `PRD` (e.g. `| PRD | \`PRD01\` v1.0 |`). The PRD file is the one in `prds` whose `Mã` header value contains that code.
- **PRD rows** (any table row, matched on its first cell after `plain`):
  - `PRDnn-US-nn`: the last cell is the priority. A story is Must when the priority starts with `Must`.
  - `PRDnn-FR-nn`: the third cell holds `US-nn` tokens.
  - `PRDnn-AC-nn`: the second cell holds an `FR-nn` token.
  - The ACs of a story are the ACs whose FR lists that story.
- **Release plan rows:** a row whose first cell is `PRDnn-REL-n`. Columns are `REL | Mục tiêu | Story | Persona | Phụ thuộc | Cỡ áo`.
  - *Story* holds entries separated by `;`. Each entry is `PRDnn-US-nn` (whole story) or `PRDnn-US-nn (AC-07, AC-08)` (part of a story).
  - Empty entries are ignored.
- **Plan:** a file in `plans` is a sprint plan when its text matches `/PRD\d+-(US|FR|AC|REL)-\d+/`. Its release plan comes from the line `**Release plan:** \`docs/backlog/<file>.md\``; the backticks are optional.

**Findings.** Each finding is exactly `<file>: <message>`, where `<file>` is the input key:

| Condition | Message |
|---|---|
| Release plan PRD code matches no PRD | `PRD <code> not found` |
| REL or story id prefix ≠ release plan code | `<REL> does not belong to <code>` / `<REL> story <US> does not belong to <code>` |
| REL id repeated | `duplicate <REL>` |
| Story not in PRD | `<REL> unknown story <US>` |
| AC not an AC of that story | `<REL> <US> lists <AC>, which is not an AC of that story` |
| Size not in S/M/L/XL | `<REL> size must be S, M, L or XL: <value>` |
| Story whole in two RELs, or whole in one and partial in another | `<US> is planned whole in <REL-a> and again in <REL-b>` |
| Same AC in two RELs | `<US> <AC> is in both <REL-a> and <REL-b>` |
| *(release plan and PRD both Approved)* Must story in no REL | `Must story <US> is in no REL` |
| *(both Approved)* Must story split but ACs uncovered | `Must story <US> is split but <AC, AC> are in no REL` |
| *(release plan Approved)* each `checkPrdApproval` finding | `<finding>` |
| Sprint plan without the line | `mentions PRD ids but has no **Release plan:** line` |
| Line points to a missing file | `release plan <path> not found` |
| Release plan not Approved | `release plan <path> is not Approved` |
| Its PRD not Approved | `PRD <code> is not Approved` |
| Plan names a REL the release plan lacks | `unknown <REL>` |

The structural checks (rows 1–8) run at every status.

- [ ] **Step 1: Write the failing tests** in `scripts/check-release-plan.test.ts`.
  - Build fixtures in the style of `scripts/check-personas.test.ts`, with helpers `prd(status, rows)`, `releasePlan(status, rels, changeLog)` and `plan(text)`.
  - Use one PRD `PRD01` with:
    - US-01 Must (FR-01 → AC-01, AC-02);
    - US-02 Must (FR-02 → AC-03);
    - US-03 Could (FR-03 → AC-04).
  - The Approved change log is `| 2026-11-25 | 1.0 | BA review | BA review |` followed by `| 2026-11-25 | 1.0 | Người dùng duyệt | Quyết định của người dùng |`.
  - Tests, each asserting `toEqual` with the exact message(s):
    - `emptyInput_passes` → `[]`
    - `coveredApprovedPlan_passes`: REL-1 = `PRD01-US-01; PRD01-US-02` → `[]`
    - `storyCellVariants_normalise`: `' PRD01-US-01 (PRD01-AC-01) ; PRD01-US-01 (AC-02);PRD01-US-02 ;'` split across REL-1 and REL-2 → `[]`
    - `unknownPrd_fails`: release plan for `PRD09` → `['PRD09-x.md: PRD PRD09 not found']`
    - `unknownStory_fails` → `<REL> unknown story PRD01-US-09`
    - `foreignAc_fails`: `PRD01-US-02 (AC-01)` → `PRD01-REL-1 PRD01-US-02 lists PRD01-AC-01, which is not an AC of that story`
    - `badSize_fails`: size `XXL`
    - `duplicateRel_fails`
    - `wholeTwice_fails`
    - `overlappingAc_fails`
    - `mustStoryMissing_fails`: only US-01 planned → `Must story PRD01-US-02 is in no REL`
    - `splitMustStoryUncovered_fails`: only `PRD01-US-01 (AC-01)` → `Must story PRD01-US-01 is split but PRD01-AC-02 are in no REL`
    - `couldStoryMissing_passes`
    - `draftReleasePlan_skipsCoverage`
    - `prdInReview_skipsCoverage`
    - `approvedWithoutUserApprovalRow_fails` → `no user-approval row for v1.0`, prefixed with the file name
    - `planWithoutPrdIds_isIgnored`
    - `planWithoutReleasePlanLine_fails`
    - `planWithMissingReleasePlan_fails`
    - `planWithDraftReleasePlan_fails`
    - `planWithUnknownRel_fails`
    - `validSprintPlan_passes`

- [ ] **Step 2: Run tests to verify they fail**

  Run: `bun test scripts/check-release-plan.test.ts`
  Expected: FAIL (module `./check-release-plan` not found).

- [ ] **Step 3: Export `headerValue`** from `scripts/check-prd-approval.ts`, then implement `checkReleasePlans` and the CLI in `scripts/check-release-plan.ts`.
  - Start the file with a 2–3 line header comment like the one in `check-personas.ts`: what it gates, the reference `docs/process/lifecycle.md` § Kế hoạch phát hành, and the usage line.
  - Respect `.claude/rules/comments.md` (`bun scripts/check-comments.ts` enforces it).

- [ ] **Step 4: Run tests to verify they pass**

  Run: `bun test scripts/` → all pass, including the existing `check-prd-approval` and `check-personas` tests.
  Run: `bun scripts/check-release-plan.ts` → exit 0 with no output on the current repo, which has no `docs/backlog/`.

- [ ] **Step 5: Commit**

  ```bash
  git add scripts/check-prd-approval.ts scripts/check-release-plan.ts scripts/check-release-plan.test.ts
  git commit -m "feat(scripts): add release plan checker"
  ```

### Task 2: Run the checker in the gates

Risk tier 2 (`scripts/test-gate.sh`, `.github/`).

Review: an Opus reviewer after this task, with about 5 mutants aimed at the Task 1 guards that this wiring makes binding:
- drop the coverage check;
- drop the AC-overlap check;
- accept a Draft release plan in a sprint plan;
- skip the `**Release plan:**` line requirement;
- drop the `checkPrdApproval` call.

Each mutant must make a Task 1 test fail.

**Files:**
- Modify: `scripts/test-gate.sh`. Add `bun scripts/check-release-plan.ts` after `bun scripts/check-personas.ts`.
- Modify: `.github/workflows/test.yml`. Add the step `Check release plans (docs/process/lifecycle.md § Kế hoạch phát hành)` running `bun scripts/check-release-plan.ts`, after the persona step.
- Modify: `.gitlab-ci.yml`. Add `- bun scripts/check-release-plan.ts` to the `test:comments` script list, after `check-personas`.
- Modify: `.claude/commands/git-accept.md` step 1. It runs `bun scripts/check-prd-approval.ts` on every changed `docs/backlog/*.md` too, except `README.md` and `_template*.md`.
- Modify: `.claude/decisions.md`. Append one entry, following that file's header rules: "release plan checker in the test gate", why, and the rejected option (extending `check-prd-approval.ts` instead).

**Interfaces:**
- Consumes: the CLI from Task 1.
- Produces: nothing new.

- [ ] **Step 1:** Make the edits listed under Files.
- [ ] **Step 2:** Prove the gate fails when it should.
  - Create a throwaway `docs/backlog/PRD09-x.md` whose header row is `| PRD | PRD09 |`, then run `bun scripts/check-release-plan.ts`.
  - Expected: exit 1 with `PRD09-x.md: PRD PRD09 not found`.
  - Delete the file.
- [ ] **Step 3:** Run `bun test scripts/ && bun scripts/check-comments.ts && bun scripts/check-personas.ts && bun scripts/check-release-plan.ts`. Expected: all exit 0.
- [ ] **Step 4: Commit**

  ```bash
  git add scripts/test-gate.sh .github/workflows/test.yml .gitlab-ci.yml .claude/commands/git-accept.md .claude/decisions.md
  git commit -m "ci: run release plan checker in test gate and CI"
  ```

### Task 3: Templates and doc indexes

Risk tier 0. Review: the main session reads the diff.

**Files:**
- Create: `docs/backlog/_template.md`
  - Header table: `Mã` (`PRDXX-REL`), `PRD` (`PRDXX` + approved version), `Trạng thái` (Draft / In review / Approved), `Phiên bản`, `Người duyệt`, `Cập nhật`.
  - `## 1. Story map`: backbone of the primary persona's activities, with stories under each.
  - `## 2. Bản giao`: the table `| REL | Mục tiêu | Story | Persona | Phụ thuộc | Cỡ áo |`, with one example row showing the `;` separator and a partial `(AC-…)` entry.
  - `## 3. Chưa xếp`
  - `## 4. Lịch sử thay đổi`: same table as the PRD template.
  - Short guidance lines under each heading carry the rules of spec § 5.2:
    - `REL-1` is a walking skeleton;
    - each REL can be demoed and none is backend-only;
    - every Must story is in exactly one REL, or split by disjoint AC sets;
    - ordering is the user's decision.
- Create: `docs/backlog/README.md`. Covers:
  - what a release plan is;
  - who writes, reviews and approves it (`product-manager`, `tech-lead` size, `business-analyst`, user);
  - that editing it does not send the PRD back to *In review*;
  - that `scripts/check-release-plan.ts` enforces it;
  - an empty *Danh mục* table `| Mã | PRD | Trạng thái | Phiên bản | REL xong |`.
- Modify: `docs/tech/_template.md`
  - Add a header row `Trạng thái theo bản giao`, with the table from spec § 5.3 placed right after the header table.
  - Add a `REL` column to §13 *Truy vết*.
  - Add one line under §1 saying which sections are common (§2, §3, §5, §7, §12) and which grow per REL (§4, §6, §8, §11).
- Modify: `docs/tech/README.md`
  - *Quy trình*: v1.0 = common part + `REL-1`; each later REL is a minor version that sends only its own row to *In review*; a change to the common part is major.
  - *Danh mục*: replace the `Trạng thái` column with `Trạng thái theo bản giao`.
- Modify: `docs/prd/README.md`. Step 7 becomes: the release plan (`docs/backlog/`, `docs/process/lifecycle.md` § Kế hoạch phát hành), then the sprint loop.

**Interfaces:**
- Consumes: the release plan table format from Task 1 (parsing rules). The template's example row must parse with no structural finding.
- Produces: the template file paths that Tasks 4–5 reference.

- [ ] **Step 1:** Write and modify the files listed above.
- [ ] **Step 2:** Copy `docs/backlog/_template.md` to `docs/backlog/PRD01-sample.md`, fill it against a sample `PRD01`, and run `bun scripts/check-release-plan.ts`. Expected: no finding other than `PRD PRD01 not found`. Delete the copy.
- [ ] **Step 3:** Run `bun scripts/check-comments.ts`. Expected: exit 0.
- [ ] **Step 4: Commit**

  ```bash
  git add docs/backlog/_template.md docs/backlog/README.md docs/tech/_template.md docs/tech/README.md docs/prd/README.md
  git commit -m "docs(process): add release plan template and per-slice tech status"
  ```

### Task 4: `lifecycle.md`, `progress.md`, process decision, Plane config example

Risk tier 0. Review: the main session reads the diff against the spec.

**Files:**
- Modify: `docs/process/lifecycle.md`. Each rule is written once, here; other files point to it with `lifecycle.md § <tên mục>`.
  - § 1 table: the stage rows of spec § 5.1, including 1b, 1c-UI, 1c, the sprint loop (2, 3, 4, 5, 6, 6b, 6c), 7 and 8. Add the start order `1 → 1b → 1c-UI → 1c → sprint 1`.
  - **New sections**, using these exact headings because other files reference them:
    - `## 1c. Kế hoạch phát hành` (spec § 5.2, including the two levels of estimation);
    - `## 1d. Nền kiến trúc` (spec § 5.3);
    - `## 1e. Vòng sprint` (spec § 5.4);
    - `## 1f. Definition of Ready và Definition of Done` (spec § 5.5);
    - `## 1g. Plane` (spec § 5.6: config file, mapping, state milestones, write rights, the no-Plane fallback).
  - § 2 *Cỡ công việc*: the S/M/L rows of spec § 5.7, plus the line about bugs found at sprint review.
  - § 3 *Cổng*: the gate is per story through DoR. PRD, release plan and runway remain module gates. A tech-doc change follows the per-REL status.
  - § 4 *Lệch thiết kế*: a change to the common part of the tech doc stops the stories in progress.
  - § 5 *Commit tài liệu*: the module docs branch carries PRD + release plan + tech v1.0 + `REL-1` design link. Each later REL gets `chore/<module>-rel-<n>-docs`. A release plan edit is a small docs PR. The sprint plan travels on `feature/*`.
  - § 6 *Quyền push và mở PR*: a "plan" at stage 4 is now a sprint plan, named `docs/superpowers/plans/YYYY-MM-DD-<module>-<SPR-…>.md`. Its header carries the sprint goal, the Cycle, the stories and ACs with points, the `**Release plan:**` line and the RELs. Phases stay the PR unit (about 1,500 lines).
  - § 7 *Truy vết*: the chain from spec § 5.9, plus the new findings: a Must story with no REL, and a PR without a `Plane:` line when `.claude/plane.json` exists.
  - § 8 *Retro*: add the sprint retro from spec § 5.4 step 5. The freeze stays: process ideas go to *Chờ retro*.
  - § 10 *Tiến độ*: the new `progress.md` columns and the *Sprint* table.
- Modify: `docs/process/progress.md`
  - Module table columns: `Module | PRD | Kế hoạch phát hành | Nền kiến trúc | UI/Tech theo REL | appsec | REL xong | Merged`.
  - New `## Sprint` table: `Cycle | Mục tiêu | Điểm cam kết | Điểm Done | Story dời | Story bị từ chối`.
  - *Số đo*: add the columns `Số sprint` and `Velocity TB`.
- Modify: `docs/process/decisions.md`. A new entry at the top, dated 2026-11-25, titled `Lập kế hoạch Agile sau PRD: kế hoạch phát hành, vòng sprint, Plane`. It records:
  - the user's choice of option B;
  - why it is applied outside a retro (no module running);
  - the rejected options A and C, one line each, from spec § 4;
  - a pointer to the spec.
- Create: `.claude/plane.example.json`

  ```json
  {
    "projectId": "",
    "projectIdentifier": "",
    "states": { "backlog": "", "todo": "", "inProgress": "", "readyForTesting": "", "testing": "", "done": "", "pending": "", "cancelled": "" },
    "labels": { "BE": "", "FE": "", "Mobile": "", "bug": "" }
  }
  ```

  § 1g says:
  - a product copies this file to `.claude/plane.json`;
  - per-module `PRDnn-REL-n` labels are created on demand and are not listed in the file;
  - without the file, every Plane step is skipped.

**Interfaces:**
- Consumes: the template paths from Task 3; the checker name and the `**Release plan:**` line from Task 1.
- Produces: the section headings `§ 1c`–`§ 1g` that Task 5 and Task 6 reference.

- [ ] **Step 1:** Edit the files listed above.
- [ ] **Step 2:** Make sure every `§` reference resolves. Run `grep -o "lifecycle.md § [^)\`,.;]*" -r docs .claude AGENTS.md | sort -u` and check that each name matches a heading in `docs/process/lifecycle.md`.
- [ ] **Step 3:** Run `bun scripts/check-comments.ts`. Expected: exit 0.
- [ ] **Step 4: Commit**

  ```bash
  git add docs/process/lifecycle.md docs/process/progress.md docs/process/decisions.md .claude/plane.example.json
  git commit -m "docs(process): add release planning, sprint loop, DoR/DoD and Plane to the lifecycle"
  ```

### Task 5: Agent prompts and `AGENTS.md`

Risk tier 0, but this is a user-merge zone. Review: the main session reads the diff.

**Files.** Each change is one or a few lines that point to `lifecycle.md § …`, not a copy of the rule:
- `.claude/agents/product-manager.md`: writes and maintains the release plan from `docs/backlog/_template.md`, including the story map and REL slicing (§ 1c). It never decides REL order; it proposes the order and lists it as a question.
- `.claude/agents/tech-lead.md`:
  - T-shirt size per REL at 1b;
  - story points at refinement, Fibonacci, at most 8 (§ 1c);
  - v1.0 = common part + `REL-1`, and the per-REL status table (§ 1d).
- `.claude/agents/business-analyst.md`, a release-plan review checklist:
  - each REL can be demoed and none is backend-only;
  - `REL-1` is a walking skeleton;
  - every Must story is covered;
  - split stories use disjoint AC sets;
  - dependencies point to earlier RELs.
- `.claude/agents/ui-designer.md`: designs one REL at a time (`REL-1` at 1c-UI, later RELs at refinement).
- `.claude/agents/solution-architect.md`: REL order and sprint scope are business decisions, so it escalates them to the user.
- `.claude/agents/implementer.md`: the brief names the sprint goal and the stories; DoD per `lifecycle.md § 1f`; the PR `## Requirements` section gets a line `Plane: <ids>` when `.claude/plane.json` exists.
- `.claude/agents/pr-reviewer.md`:
  - it checks the code against the approved version of the common part and of the story's own REL (§ 1d);
  - a missing `Plane:` line, when `.claude/plane.json` exists, is `blocking`.
- `AGENTS.md`:
  - Software Lifecycle table: add rows 1b, 1c-UI, 1c, 6b and 6c; stage 4 becomes the sprint plan.
  - One sentence under the table: only the main session writes to Plane, per `lifecycle.md § 1g`.

**Interfaces:**
- Consumes: the `§ 1c`–`§ 1g` headings from Task 4.

- [ ] **Step 1:** Edit the files listed above.
- [ ] **Step 2:** Run the same `§` reference check as Task 4 step 2. Expected: every reference resolves.
- [ ] **Step 3: Commit**

  ```bash
  git add .claude/agents/product-manager.md .claude/agents/tech-lead.md .claude/agents/business-analyst.md .claude/agents/ui-designer.md .claude/agents/solution-architect.md .claude/agents/implementer.md .claude/agents/pr-reviewer.md AGENTS.md
  git commit -m "docs(agents): wire release plan, sprint loop and Plane into agent prompts"
  ```

### Final gate and PR (main session)

- [ ] Run `scripts/test-gate.sh`. Expected: exit 0.
- [ ] Run `bun scripts/risk-tier.ts --base origin/dev`. Expected: tier 2.
- [ ] Call `pr-reviewer` with `model: opus` on the full branch. Do this after the push, only on the user's order.
- [ ] **PR:**
  - Title: `chore(process): agile planning after PRD (release plan, sprints, Plane)`.
  - `## Changelog`: `- Added: Release planning, sprint loop with DoR/DoD, and Plane tracking in the development process.`
  - Then the `Requirements`, `Lệch nhỏ so với thiết kế`, `Verification` and `Risk tier` sections.
  - The user merges.
