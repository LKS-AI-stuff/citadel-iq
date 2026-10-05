---
name: implement-technical-design
description: Implement a technical design document from .claude/technical-designs/ — writes the actual code changes, validates with build/typecheck/lint (fixing failures automatically), then has a separate agent on a different model review the change. Use when the user asks to implement/build a technical design, or invokes /implement-technical-design. Takes the design filename as an argument; if none is given, ask which file to use.
---

# Implement a technical design

This skill takes a design doc from `.claude/technical-designs/` and actually builds it — real code
changes across the affected layers, followed by build/typecheck/lint validation with automatic
fix-and-retry, a self-review against the design's own checklist, and an independent review by a separate agent/model before reporting done.

No hooks or CI are involved in this workflow. Every step below runs inline, by whoever invokes this
skill, in this one skill-run. Completion is surfaced via the native `PushNotification` tool (step
6), not a custom hook — it already reaches the desktop and, if Remote Control is connected, the
user's phone.

## 1. Resolve the input file

- If a filename was passed as an argument, use it. Accept it with or without a `.md` extension,
  and with or without the `.claude/technical-designs/` prefix.
- If no filename was given, list the files currently in `.claude/technical-designs/` and ask the
  user which one to use (`AskUserQuestion`, one option per file).
- Resolve the final path as `.claude/technical-designs/<name>.md`. If it doesn't exist, say so,
  list what's actually there, and ask again rather than guessing.

## 2. Read for context before writing any code

Read, in order:

1. The resolved design doc itself.
2. `CLAUDE.md` — architecture, conventions, "Important implementation principles." Every change
   this skill makes must respect Clean Architecture (Domain → Application → Infrastructure → Api,
   dependencies point inward), keep business logic out of controllers, put external dependencies
   behind interfaces, use configuration instead of hardcoded values, and keep I/O async.
3. The actual current code the design touches — don't trust the design doc's "current state"
   section blindly; the code may have moved on since it was written. Use `Explore` or targeted
   `Grep`/`Read` calls to confirm what's really there before changing it.

If the design's own description of the current state conflicts with what the code actually shows,
follow the code and note the discrepancy when reporting back at the end.

## 3. Implement in phases

Work through the design's own "Implementation phases" section if it has one, in order. If it
doesn't, implement in this default order and don't skip a layer the design actually needs:

```
Domain → Application → Infrastructure → Api → Frontend
```

Make each phase a complete, coherent change before moving to the next — don't leave a layer
half-wired. Follow the codebase's existing patterns exactly (see how the delete/rename features,
or any other recent feature, were implemented layer-by-layer) rather than introducing a new style.

## 4. Validate — fix and retry automatically

After implementation, run the validations that apply to what changed:

- **Backend touched:** `dotnet build` on the solution (`CitadelIQ.slnx`).
- **Frontend touched:** `npx tsc -b` (typecheck), `npm run lint`, `npm run build`.
- **API behavior changed:** where practical, verify with `curl` against a locally-started instance
  (start it, exercise the changed endpoint(s), stop it afterward) — the way delete/rename were
  verified. There is no browser-automation tool in this environment, so UI interaction cannot be
  clicked through; say so explicitly in the final report rather than claiming it was tested.

**On any failure:** read the actual error output, fix the root cause, and re-run that same
validation step. Retry up to **2 additional times** per failing step (3 attempts total). If it's
still failing after that, stop implementing further phases, report the exact failure and what was
tried, and ask before continuing — don't loop indefinitely on the same error.

Always leave dev servers stopped when done (kill anything started for curl verification).

## 5. Review: self-review, then an independent agent review

### 5a. Self-review against the design

Read back the **full** diff (`git diff` plus the contents of untracked files from `git status` — don't
rely on memory of what was written) and check it against the design doc's own "Non-functional
considerations" and any explicit checklist/"Definition of done" it contains. Fix anything that's
clearly just an oversight against the design's stated requirements; flag (don't silently fix) genuine
judgment calls or scope questions.

### 5b. Independent code review by a separate agent

A review by the same session that wrote the code shares its blind spots, so also hand the change to a
fresh agent. This step is explicitly part of this skill — it does not need separate user approval.

1. Spawn one agent via `Agent` with `subagent_type: "general-purpose"` and a **different model from
   the one doing the implementation** (e.g. `model: "opus"`, or `"fable"` if the implementer is
   already Opus; pick whichever differs). Do not use `fork` — a fork inherits this conversation's
   context and would not be independent. Run it in the foreground; the result is needed before
   reporting.
2. The prompt must be self-contained (the agent has none of this context). Include: the design doc
   path; that it should read `CLAUDE.md` and the design; that it should inspect the change with
   `git status` / `git diff` and read untracked files; that it is **read-only** (no edits, no
   commits, no starting servers); and that it should report findings, most severe first, each with
   `file:line`, what is wrong, and a concrete failure scenario. Ask it to check specifically for:
   - **Correctness bugs** — logic errors, race conditions, async/cancellation mistakes, resource leaks,
     edge cases (empty/null inputs, boundaries).
   - **Design conformance** — requirements, API contract, config keys/defaults and "Definition of
     done" items that were missed or implemented differently.
   - **Architecture rules** from `CLAUDE.md` — Clean Architecture dependency direction, no business
     logic in controllers, external dependencies behind interfaces, config not hardcoded, no secrets
     or internal details leaked in responses or logs.
   - **Security** — injection, unsafe rendering, input limits, rate limiting gaps.
   - **Test gaps** — behaviour the design requires tests for that is untested or only trivially tested.
   - **Frontend** — typing holes, stale state/effects, accessibility, MUI v9 `sx`-only layout rule.
   Tell it to skip style nitpicks and anything the build/lint already enforces, and to say plainly
   when it finds nothing rather than inventing issues.
3. Triage the findings yourself — don't apply them blindly. For each: verify it against the code, then
   - **fix** it if it's a real defect or a clear miss against the design, and re-run the affected
     step-4 validations (same 3-attempt limit);
   - **skip** it if it's wrong or a matter of taste, with a one-line reason;
   - **flag** it for the user if it's a judgment call or scope question.
4. Do not loop: one review round only. If fixes were substantial, say so in the report and suggest
   `/code-review ultra` rather than spawning another round.
5. If the agent fails or can't run, say so in the report — never claim an independent review happened
   when it didn't.

The final report must list what the reviewer found and what was done with each finding (fixed /
skipped with reason / flagged).

## 6. Notify, then report — don't commit

Call `PushNotification` (`status: "proactive"`) with a short, single-line, under-200-character
message summarizing the outcome — e.g. `"phase-1-design implemented: build/lint/tests pass"` or, on
a stopped failure, `"phase-1-design implementation stopped: <short reason>, needs your input"`. This
is exactly the kind of moment that tool exists for: a task that ran while the user may have stepped
away just finished, or hit something needing their decision. Send it regardless of whether this
step succeeded or stopped early — both are worth knowing about. Don't send it if the design doc
turned out to need no real changes (nothing to report) or if the run stopped immediately at step 1
for lack of a valid filename — no notification-worthy work happened.

Then summarize what changed (by layer/file, briefly — not a full diff dump) and the validation
results (what passed, what was retried and why, what couldn't be verified). Do not create a git
commit — only commit when the user explicitly asks. Include the reviewer's findings and their
disposition (see 5b). Mention, as a one-line suggestion (not something to run automatically), that
`/code-review ultra` is available afterward for a deeper multi-agent cloud review if they want one.
