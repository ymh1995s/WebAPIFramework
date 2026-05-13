# Web API Framework

Root CLAUDE.md. Sub-projects may have their own CLAUDE.md, but this document takes precedence.

## Project List

- **Framework.Api**: ASP.NET Core Web API (EF Core backend)
- **Framework.Admin**: Blazor Server admin tool
- **Framework.Application**: Use cases, workflows, domain orchestration
- **Framework.Domain**: 엔티티, Value Object, Enum, 도메인 인터페이스. `Common/` 하위에 Result&lt;T&gt;, Guard, DomainException 베이스 등 도메인 프리미티브 포함. 다른 Framework 프로젝트를 참조하지 않음
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

### [Convention] Admin HTTP client pattern
- **모든 Admin Blazor 페이지**는 `IHttpClientFactory` 대신 `ApiHttpClient` (`Framework.Admin/Http/ApiHttpClient.cs`)를 주입하여 사용한다.
- `ApiHttpClient`는 `"ApiClient"` 명명 클라이언트(AdminApiKeyHandler + HttpLogCaptureHandler 체인 포함)를 내부에서 생성하고, `AdminJsonOptions.Default`(camelCase enum 직렬화)를 일관 적용한다.
- DI 명명: `[Inject] private ApiHttpClient ApiClient { get; set; } = default!;`
- `IHttpClientFactory` 직접 주입은 `InquiryTest.razor.cs`처럼 `SendAsync` + Authorization 헤더 직접 조작이 필요한 경우에만 예외적으로 허용한다.

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
- **Exception — programmer agent**: programmer is always run in foreground (`run_in_background: false`) so the user can approve each file operation interactively.
- **Orchestrator MUST NOT directly write or edit production code files** (Write/Edit tools on `.cs`, `.razor`, `.razor.cs`, etc.). All code changes MUST go through the programmer agent. The only exceptions are: CLAUDE.md itself, documentation files, and configuration files explicitly requested by the user.

### programmer → qa-reviewer Auto-Loop (Orchestrator MUST)

Run autonomously the moment programmer finishes. Do not ask the user.

1. Invoke qa-reviewer (pass only the files programmer reported as changed).
2. Approved → loop ends. **[Required]** The orchestrator MUST report results to the user before any next step.
3. Rejected → re-invoke programmer (forward rejection reasons) → back to step 1. **Max 3 iterations**; on overflow, report unresolved issues and stop.

- During the loop, the Collaboration Protocol Write/Edit approval gate is waived (but any new file outside the initially approved file list still requires explicit confirmation).
- Do not report intermediate progress (during implementation or review) to the user. **However, reporting the final approval result is mandatory and must not be omitted.**

### qa-reviewer 테스트 실행 범위 정책 (Orchestrator MUST)

qa-reviewer 호출 시 orchestrator는 호출 명세에 `dotnet test` 범위를 명시한다.
1인 개발 환경에서 매 사이클 전체 실행은 과하므로 변경 범위에 비례한 검증으로 비용을 조절한다.

- **전체** — 공용 영역(RewardDispatcher / AppDbContext / IUnitOfWork / Domain Common / Migration 신규 / Repository 인터페이스) 변경, 변경 파일 수 ≥ 8, 또는 사용자가 "전체 회귀" 명시 시
- **영역 필터** (`--filter "FullyQualifiedName~{SUT}"`) — 기본값. Auto-Loop 2차 이후 항상 이 모드
- **빌드만** — `.razor` Admin UI / 주석·문서 전용 변경, 또는 사용자가 "테스트 스킵" 명시

자체 판단 절차와 공용 영역 자동 감지 안전망은 `.claude/agents/qa-reviewer.md` 참조.