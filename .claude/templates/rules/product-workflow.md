---
paths:
  - "{{PRD_DIR}}/**"
  - "{{DESIGN_DIR}}/**"
  - "plans/**"
---

# Product Workflow (PRD → Dev)

Read this when starting work on a business module/screen defined in `{{PRD_DIR}}/`, or when
touching `{{DESIGN_DIR}}/` or `plans/`.

## Source of Truth

- `{{PRD_DIR}}/` is the PRD baseline. Its convention file (`{{PRD_CONVENTION_FILE}}`) defines the
  module↔screen map, the document status lifecycle and the screen-code convention — read it first
  rather than assuming module/screen codes; they change as the convention evolves.
- A module/screen is dev-ready only once its PRD's status is `FINAL`. `Draft`/`Reviewed` are not
  implementation-ready.
- `{{DESIGN_DIR}}/` is the visual reference, not the behavior source — PRD text always wins on
  business rules. Prefer an interactive prototype over a static image, and a primary export folder
  over a renamed-copy or raw-upload folder (often stale duplicates — verify before trusting). A PRD
  copy bundled inside a design export is a snapshot; `{{PRD_DIR}}/` stays authoritative.
- Technical designs: new ones go in `docs/superpowers/specs/` (see CLAUDE.md § Superpowers
  Integration); `plans/implements/` holds the earlier numbered ones — continue its numbering only
  when adding to that series.

## Before Writing Code

1. Confirm the PRD is `FINAL`.
2. Reconcile the PRD against current code and current design, requirement by requirement: sort each
   into _already matches_ (reuse), _needs changing_, or _needs removing_. Don't assume greenfield —
   code inherited from the starter kit may already do something adjacent, or something the PRD
   forbids.
3. For each screen, cross-check its design against the PRD's actual field/section/tab list for that
   screen — not just that a file with the right name exists. A design can predate a PRD revision
   (check the PRD's changelog for reversed decisions).
4. An open/undecided PRD item, or a conflict between two currently-effective documents, blocks only
   the directly-dependent part of the work. Stop that part and ask — don't infer a business rule to
   keep moving.

## Traceability (ID Spine)

Carry the PRD's module code and screen code through the whole chain: branch name, commit scope,
tracker item title, test names/comments — so "which commit implements AC-04 of screen X" is
answered by search, not memory.

## Task Breakdown

Before creating a tracker project/board for this product, check whether one exists and reuse its
module/grouping structure.

**One screen = one parent item plus a decomposition**, not a single item: one item for a whole
screen can't be estimated, can't be split between two people, and can't show which part is blocked
by which open question. Title the parent with the screen code and the screen's name in
`{{PO_LANGUAGE}}` (e.g. `S2.1 — <screen name>`). Skip the decomposition only for a genuinely small
screen (a single form, a read-only detail panel).

Decompose by **shippable slice**, not by technical layer alone:

- Each child is independently codeable and reviewable, and maps to a named group of AC.
- Split backend/frontend when both carry real work; within each, split by functional block (domain
  model · shared business rule · API group · screen shell · filter bar · display blocks ·
  states/errors).
- **A business rule other modules will call gets its own child**, never buried in a controller or a
  view — otherwise the later module reimplements it and the two drift apart.
- Each child ≤ {{MAX_CHILD_POINTS}} points; a bigger one means the decomposition isn't finished.
- Prefix child titles with the screen code: `S2.1/BE-1 — …`, `S2.1/FE-3 — …`.

Copy the PRD's Acceptance Criteria into the parent verbatim (in the PRD's own language) rather than
re-deriving them. Each child's description states its scope, the BR/AC it must not violate, the AC
it carries, and what blocks it.

Then **reconcile the union of the children's AC against the PRD's full AC list**. Children are what
gets acceptance-tested, so an AC no child claims is a condition nobody checks. Every unclaimed AC
needs a stated reason (belongs to another module's screen, only verifiable once the destination
screen exists…), and AC of the same nature get the same treatment. Record the decomposition table
(item · points · hours · dependencies · total), the critical path and the AC reconciliation at the
end of the technical design.

Set on every item created this way:

- **Assignee** — the requesting user, resolved at creation time (the tracker's "me"), never a
  hardcoded person.
- **Cycle/sprint** — the active one, resolved by date containment (start/end actually bracketing
  today), not by a "current" flag alone — a stale cycle can stay flagged current past its end.
- **State** — `{{STATE_TODO}}` on parent and children: a technical design plus a decomposition is
  what makes an item ready to queue. Unresolved questions go in the description, not into a backlog
  state.
- **Estimate** — on the **children only** (the parent stays blank, so nothing is double-counted),
  on the project's configured scale ({{POINT_SCALE}}). Points measure relative complexity; the
  point→hours conversion ({{POINTS_TO_HOURS}}) carries the team's delivery speed. When the speed
  changes, change the conversion — it keeps existing estimates comparable. Re-baselining the points
  is only correct applied to every item at once, keeping their order, with the date recorded in the
  technical design (velocity read across that date is otherwise wrong). If the project has no
  estimate scale yet, raise it with the user — don't invent one, and don't silently skip the field.
- **Label** — `{{LABEL_PARENT}}` on the parent, `{{LABEL_CHILD}}` on every child; match the existing
  taxonomy, don't create a parallel one.

### If the tracker is Plane: resolving estimate-point ids

`project_estimate` (`retrieve` and `list_points`) can return HTTP 404 on a project that uses
estimates — an endpoint gap, **not** a missing scheme; check the `estimate` field of
`project retrieve` instead. To recover a point value's UUID, read `workitem_activity list` on items
that already carry one: an `estimate_points` entry gives the displayed value (`new_value`) and the
UUID (`new_identifier`). That only recovers values someone has used — for an unused one, ask the
user. Plane's `point` field (capped at 12) is unrelated; never use it in place of the scheme's
`estimate_point`.

## Status Transitions

- `FINAL → In Dev`: when work starts on the module's first screen — mechanical.
- `In Dev → Done`: business acceptance — needs explicit product-owner confirmation, never
  self-declared from passing tests.

## Decisions

A structural trade-off made while turning a requirement into a technical design follows the
`.claude/decisions.md` gate. A **product ruling** (the PO settles a name, a scope boundary, an
accepted gap, a threshold) goes in `{{PO_DECISION_PATH}}` (CLAUDE.md § PO Decision Log), no gate —
but only when the PO actually ruled on it; your own judgment call during reconciliation is not a PO
decision.
