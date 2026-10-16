# Task Tracking ({{TRACKER}})

Applies to **every** coding task in this repo, not just PRD-driven work. The tracker is the shared
view of what is being built right now — an item left in `{{STATE_TODO}}` while its code is already
merged makes the board useless to everyone else.

Project: **{{PROJECT_NAME}}** — identifier `{{PROJECT_IDENTIFIER}}`, id `{{PROJECT_ID}}` (a
workspace can hold several projects; never operate on a different one by accident).

## Before writing code

1. **Identify the work item** the change belongs to. If the user named one
   (`{{PROJECT_IDENTIFIER}}-42`, a screen code like `S2.1/BE-1`, a title), resolve it by identifier
   or by search.
2. If no item was named, look for it — open items assigned to the current user, or a keyword
   search on the request — and **confirm the match with the user** before touching it.
3. If nothing matches, say so and ask. **Never create a work item unprompted** — choosing the
   parent/child split, estimate, label, cycle and assignee is a bigger decision than the coding
   task at hand.
4. Read the item's description and AC before coding — they are part of the requirement.
5. Move it to **`{{STATE_IN_PROGRESS}}`** when implementation actually starts (not while still
   orienting or planning).

## After finishing

A green test suite does **not** by itself move the item. Move it to
**`{{STATE_READY_FOR_TESTING}}`** only when the user asks to open a PR into `dev` (or `main` for a
hotfix) — a tester shouldn't see it as ready before there is a PR to test. Until then, leave it in
`{{STATE_IN_PROGRESS}}`.

When that request comes, comment on the item (in `{{COMMENT_LANGUAGE}}`) what was implemented and
which branch/commit carries it — the tester needs to know where to look.

If the work spans several items, transition each one as its own scope completes; don't flip a
parent while its children are still open.

## States this agent may set

| State                         | When                                                     |
| ----------------------------- | -------------------------------------------------------- |
| `{{STATE_IN_PROGRESS}}`       | Implementation started                                   |
| `{{STATE_READY_FOR_TESTING}}` | The user asks to open a PR into `dev` or `main` (hotfix) |

Everything else is somebody else's call:

- **`{{STATE_DONE}}`** is business acceptance and needs explicit product-owner confirmation —
  passing tests are never enough. Never set it on your own.
- Any other state — only when the user explicitly asks.

## Mechanics

- Look state ids up by name at update time; never hardcode them in code, docs or memory — they are
  per-project and change with any board edit.
- Match state names exactly as the tracker spells them (`{{STATE_READY_FOR_TESTING}}`, including
  capitalization).
- **Plane:** `workitem update`'s `state` takes a UUID — resolve it with `state list` for
  `{{PROJECT_ID}}`.
