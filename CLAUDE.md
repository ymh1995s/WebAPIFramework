# Web API Framework

Root CLAUDE.md. Sub-projects may have their own CLAUDE.md, but this document takes precedence.

## Project List

- **Framework.Api**: ASP.NET Core Web API (EF Core backend)
- **Framework.Admin**: Blazor Server admin tool
- **Framework.Application**: Use cases, workflows, domain orchestration
- **Framework.Domain**: Entities, value objects, enums, interfaces — domain core
- **Framework.Infrastructure**: EF Core DbContext, repositories, external integrations

## Technology Stack

- **Game Engine**: Unity
- **Framework**: ASP.NET Core
- **Language**: C#
- **Version Control**: Git (trunk-based)
- **Build / Asset**: Unity Build Pipeline / Import System

## Coding Rules

- All code MUST include **Korean comments** explaining the purpose of variables, functions, and key logic flow.
- Do not write English comments except for external library/API names.
- Code without Korean comments is considered incomplete.

## Notice (Developer TODO)

For pre-deployment replacements, unimplemented items, index plans, and feature status, see [DEVNOTES.md](DEVNOTES.md).

### [Caution] Temporary code in repository
- `Framework.Api/Program.cs` `#if DEBUG` block — debug-build-only auth bypass (PlayerId fixed to 1). Excluded from Release compilation.
- `Framework.Admin/Program.cs` `#if DEBUG` block — debug-build-only Admin auto-login. Excluded from Release compilation.

---

## Behavioral Guidelines

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

Proactiveness is allowed only within the scope the user requested. For anything outside that scope, ask first.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

Workflow: **query current state → one step at a time → verify result → report concisely**. If blocked, do not retry the same code — break the problem down differently.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

### 5. Tone & Output

- Respond in the same language as the user.
- Lead with the conclusion or action; reasoning after. No filler.
- Keep non-tool text under 4 lines unless detail is requested.
- If you cannot help, don't over-explain — 1–2 sentences plus an alternative.
- Do not use emojis unless explicitly requested.
- If a tool result is truncated, retry with a narrower query or pagination.
- If a tool result starts with `ERROR:`, analyze the cause and fix it. Do not retry the same code.

Focus text output on:
- Decisions that need user input
- Status summaries at natural milestones
- Errors or blockers that change the plan

### 6. Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question → Options → Decision → Draft → Approval**.

- Before Write/Edit, ask "**May I write this to [filepath]?**"
- Show a draft or summary, then request approval.
- Multi-file changes require explicit approval for the full changeset.
- No commits without explicit user instruction.
- When presenting options, **print the full content (tables, per-option descriptions)** before asking the user to choose. Compressed "A/B?" prompts are forbidden.

---

## Agent Auto-Dispatch Rules

- Design request → architect / Implementation request → programmer / Verification request → qa-reviewer / Security review → security-master
- Design → Implementation → Verification runs sequentially. qa-reviewer + security-master may run in parallel.
- **All agents MUST run in the background (`run_in_background: true`) unless the user explicitly requests foreground execution. No exceptions.**
- **Exception — file write permission required**: If a programmer agent needs to write/edit files and background execution prevents permission approval, re-invoke as foreground (`run_in_background: false`) so the user can approve each file operation interactively.

### programmer → qa-reviewer Auto-Loop (Orchestrator MUST)

Run autonomously the moment programmer finishes. Do not ask the user.

1. Invoke qa-reviewer (pass only the files programmer reported as changed).
2. Approved → loop ends. **[Required]** The orchestrator MUST report results to the user before any next step.
3. Rejected → re-invoke programmer (forward rejection reasons) → back to step 1. **Max 3 iterations**; on overflow, report unresolved issues and stop.

- During the loop, the Collaboration Protocol Write/Edit approval gate is waived (but any new file outside the initially approved file list still requires explicit confirmation).
- Do not report intermediate progress (during implementation or review) to the user. **However, reporting the final approval result is mandatory and must not be omitted.**