---
name: generate-technical-design
description: Generate a technical design document (in .claude/technical-designs/) from a requirements file in .claude/requirements/. Use when the user asks to create/generate/write a technical design from a requirements doc, or invokes /generate-technical-design. Takes the requirements filename as an argument; if none is given, ask which file to use.
---

# Generate a technical design from a requirements file

This skill turns a requirements doc living in `.claude/requirements/` into a grounded technical
design doc in `.claude/technical-designs/` — grounded meaning it reflects the actual current
CitadelIQ codebase, not just a restatement of the requirements in different words.

## 1. Resolve the input file

- If a filename was passed as an argument, use it. Accept it with or without a `.md` extension,
  and with or without the `.claude/requirements/` prefix.
- If no filename was given, list the files currently in `.claude/requirements/` and ask the user
  which one to use (`AskUserQuestion`, one option per file).
- Resolve the final path as `.claude/requirements/<name>.md`. If that file doesn't exist, say so,
  list what's actually there, and ask again rather than guessing.

## 2. Read for context before writing anything

Read, in order:

1. The resolved requirements file itself — this is the source of truth for *what* is needed.
2. `CLAUDE.md` — the project's architecture, conventions, and "Important implementation
   principles" section. Every design this skill produces must respect Clean Architecture
   (Domain → Application → Infrastructure → Api, dependencies point inward) and the other stated
   principles (business logic out of controllers, external dependencies behind interfaces,
   configuration not hardcoded, async I/O, etc.).
3. The existing files already in `.claude/technical-designs/` — skim them for tone and structure so
   the new doc reads consistently with what's already there, and to avoid contradicting a decision
   already recorded (e.g. don't propose something `design.md` already explicitly deferred without
   calling that out).
4. The actual current code relevant to the requirement — the real Domain entities, Application
   interfaces/services, Infrastructure implementations, and Api controllers it touches. Use
   `Explore` or targeted `Grep`/`Read` calls for this; don't skip it. A technical design that
   doesn't cite real file paths, real interface names, and real current behavior is not grounded —
   it's just the requirements doc reworded, and that's not useful to hand to an implementer.

## 3. Write the design

Structure the new document with these sections (adapt names/order slightly if the requirements
file's shape calls for it, but cover all of this ground):

- **Objective** — one or two sentences: what this design achieves, referencing the source
  requirements file by name.
- **Current state** — what exists today, specifically. Name real classes/interfaces/files. This is
  what makes the doc a *design* and not a paraphrase.
- **Proposed design, by layer** — Domain changes, Application changes (interfaces/services/DTOs),
  Infrastructure changes, Api changes. Call out anything that's a genuine architectural decision
  (not just "add a class") and give the reasoning, the way `design.md`'s "Decisions made during
  planning (with rationale)" section does.
- **Data model changes** — new/changed entities, fields, relationships, or schema, if applicable.
- **API contract changes** — new/changed endpoints, request/response shapes, if applicable.
- **Non-functional considerations** — anything touching error handling, security, performance,
  config, or the project's other stated principles.
- **Open questions / risks** — genuine unknowns or tradeoffs worth flagging, not filler.
- **Implementation phases** — a short, ordered list of concrete steps, matching this project's
  "build incrementally, verify after each stage" approach (see `design.md`'s own build-order
  sections for the tone to match).

Keep it as long as it needs to be to be genuinely useful and no longer — a design for a small
requirements file should be small; don't pad it out to look thorough.

## 4. Naming and writing the file

Derive the output filename from the input:

- If the input's base name ends in `-requirements` (or is exactly `requirements`), replace that
  with `-design` (or `design`) — e.g. `phase-1-requirements.md` → `phase-1-design.md`.
- Otherwise, keep the same base name — e.g. `postgresql-pgvector.md` stays
  `postgresql-pgvector.md` (it already doesn't say "requirements," matching the existing file of
  that name already in `.claude/technical-designs/`).
- Always kebab-case, always `.md`, never a space in the filename.

Before writing, check whether `.claude/technical-designs/<output-name>.md` already exists. If it
does, tell the user and confirm (`AskUserQuestion`) whether to overwrite it, merge into it, or pick
a different output name — never silently clobber an existing design doc, since these can carry a
lot of hand-refined detail (see how much correction `postgresql-pgvector.md` needed after its first
draft).

## 5. Report back

After writing, tell the user the output path and give a short (2-4 sentence) summary of what the
design covers — not the full contents restated, just enough for them to know whether to open and
review it next.
