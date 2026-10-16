---
name: product-module-start
description: Prepare one PRD-driven module/screen for implementation — reconcile the FINAL PRD against existing code and design, draft a technical-design stub, then create a parent tracker item and decompose it into estimated children. Use when starting work on a screen whose PRD is FINAL.
disable-model-invocation: true
---

# Product Module Start

Takes one business screen (by its PRD screen code, e.g. `S0.1`) from "PRD is FINAL" to "ready to
code". This skill **writes no code** — it stops at preparation. The principles it applies are in
`.claude/rules/product-workflow.md`.

## Workflow

```
Task Progress:
- [ ] 1. Confirm the PRD is FINAL
- [ ] 2. Reconcile the PRD against existing code
- [ ] 3. Reconcile the PRD against existing design
- [ ] 4. List the open questions
- [ ] 5. Write the technical-design stub
- [ ] 6. Create the parent item and decompose it into children
- [ ] 7. Report to the user
```

## 1. Confirm the PRD is FINAL

Read the module's PRD in `{{PRD_DIR}}/` (use the convention file to find the right one). Its
status must be `FINAL`. `Draft`/`Reviewed` → stop, tell the user, do nothing further.

## 2. Reconcile against existing code

Sort every business requirement / acceptance criterion of the screen into exactly one group:

- **Already matches** — existing code/entities satisfy it; reuse, don't rewrite.
- **Needs changing** — exists but deviates from the PRD.
- **Needs removing** — existing code does something the PRD forbids or that is out of scope.

Don't assume greenfield: infrastructure inherited from the starter kit (auth, organizations,
notifications…) may do something different from — or contrary to — this module's PRD.

## 3. Reconcile against existing design

If `{{DESIGN_DIR}}/` exists, read it in the rule's order of preference (interactive prototype first,
then the primary export, raw uploads/copies last). Compare the design's **actual content** (labels,
tabs, fields) with the PRD's description of that screen — not just the file name. A correctly named
file can show another screen's content if the PRD reversed a decision after the export (check the
PRD's changelog).

A mismatch, or no design at all, is an open question for step 4 — don't infer a layout to keep
going.

## 4. List the open questions

Collect everything from steps 2–3 that needs the PO/user: conflicts between currently-effective
documents, items the PRD marks undecided, missing/mismatched designs, existing code the PRD never
mentions (keep or remove?). This list goes into the step 7 report — don't pick an interpretation
while any point is unresolved.

## 5. Write the technical-design stub

Create one file in `docs/superpowers/specs/`, named with the module/screen code for traceability.
Minimum content: planned entities/DTOs, planned endpoints, every open question from step 4, and the
decomposition table from step 6b. It's a starting point, filled in as questions get answered.

## 6. Create the tracker items

Check whether the tracker already has a module/grouping for this product and reuse it.

### 6a. Parent (screen level)

One item titled with the screen code; its description links the technical design (step 5) and
copies the related AC checklist from the PRD verbatim. Open with the blocking questions and the AC
that can't be accepted yet because they depend on another module. The parent is a **container**:
label `{{LABEL_PARENT}}`, **no estimate**.

### 6b. Children — required unless the screen is genuinely small

Decompose by shippable slice as the rule describes: each child independently codeable and
reviewable, mapped to a named AC group; split backend/frontend and then by functional block; a
business rule other modules will call is its own child; each child ≤ {{MAX_CHILD_POINTS}} points;
titles prefixed `S2.1/BE-1 — …`. Each description gives the scope, the BR/AC it must not violate,
**the AC it carries**, and what blocks it.

Then reconcile the union of the children's AC against the PRD's full AC list; every unclaimed AC
gets a stated reason, and AC of the same nature are treated alike. Append the decomposition table
(item · points · hours · dependencies · total), the critical path and the AC reconciliation to the
technical design.

### 6c. Fields on every item (parent and children)

Assignee = the user running this skill (the tracker's "me"), cycle = the active one by date
containment, state = `{{STATE_TODO}}`, estimate on children only, labels `{{LABEL_PARENT}}` /
`{{LABEL_CHILD}}`, and the same tracker module for all. Details and the Plane estimate-id
workaround are in the rule.

## 7. Report

Summarise for the user: the PRD read, the step 2–3 reconciliation, the open questions, the path of
the technical design, and the parent + children created with their total points/hours.

Don't start coding in the same turn unless the user confirms no open question is blocking.
