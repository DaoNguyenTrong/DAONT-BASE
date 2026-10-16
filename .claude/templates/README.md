# Process templates (inactive)

Workflow rules for a fork that has a product owner, PRDs and a work tracker. The template itself
has none of these, so nothing here is loaded: Claude Code reads `.claude/rules/` and
`.claude/skills/`, not this folder. Activate a piece by copying it into place and filling its
`{{PLACEHOLDERS}}` (`grep -rnE '\{\{[A-Z_]+\}\}' .claude/rules .claude/skills .claude/hooks CLAUDE.md` must come back empty).

| File                                   | What it adds                                                                                                                                        |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `rules/task-tracking.md`               | Every coding task finds its tracker item first and moves it through the states the agent may set                                                    |
| `rules/product-workflow.md`            | PRD → dev: source-of-truth order, the FINAL gate, reconciling a PRD against code and design, the ID spine, decomposing a screen into sized children |
| `skills/product-module-start/SKILL.md` | Walks one screen from a FINAL PRD to a technical-design stub and a decomposed set of tracker items (user-invoked only)                              |
| `PO-decision.md`                       | Log of rulings the product owner made — product behavior, not system design                                                                         |

## Activating task tracking

1. Copy `rules/task-tracking.md` to `.claude/rules/` and fill the tracker, project and state
   placeholders.
2. In `CLAUDE.md` § Pre-Task Protocol, insert after step 1:

   > **Tracker** — identify the work item this change belongs to, read its description/AC, and
   > move it to `{{STATE_IN_PROGRESS}}` when implementation starts (see `task-tracking.md`)

   and before "Log decisions":

   > **Tracker (close out)** — move the work item to `{{STATE_READY_FOR_TESTING}}` only when the
   > user asks to open a PR into `dev` (or `main` for a hotfix), with a comment saying what was
   > implemented; never set `{{STATE_DONE}}` yourself

   Then renumber, and update "after Pre-Task steps 1–4" in § Superpowers Integration (the Tracker
   step makes it 1–5). Add "tracker state changes" to its Gated side effects bullet.

3. Make the same two additions to the one-line protocol in `.claude/hooks/pre-task-reminder.sh` —
   it is injected on every prompt and is easy to forget.
4. Add a `task-tracking.md` row to `CLAUDE.md` § Rule Index.

## Activating the product workflow

1. Copy `rules/product-workflow.md` to `.claude/rules/` and `skills/product-module-start/` to
   `.claude/skills/`. Fill the PRD/design folder placeholders (and the `paths:` front matter that
   scopes the rule to them), the point scale and the labels.
2. Copy `PO-decision.md` to `{{PO_DECISION_PATH}}` (e.g. `shared/docs/PO-decision.md`) and add
   this section to `CLAUDE.md`, after § Decision Log:

   > ## PO Decision Log
   >
   > Record business/product rulings the product owner has made in `{{PO_DECISION_PATH}}` — naming,
   > scope boundaries, accepted data or feature gaps, threshold/rule approvals. Distinct from the
   > Decision Log above: a PO decision is about **what the product does**, not **how the system is
   > designed**. No confirmation gate — it records a ruling that already happened, not a proposal.
   > Log only what the PO actually ruled (a PRD changelog, a tracker comment, the user's own words),
   > never your own implementation judgment. Write each entry in `{{PO_LANGUAGE}}` a non-technical
   > PO can read at a glance — no code symbols, file names or line numbers: what was decided, the
   > rejected alternative (if any), and its date (if known).

3. Add `{{PO_DECISION_PATH}}` to Pre-Task step 1 (Memory) next to `.claude/decisions.md`, and add a
   `product-workflow.md` row to § Rule Index.
4. Task tracking (above) is a prerequisite — the skill creates tracker items.
