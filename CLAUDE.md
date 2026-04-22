# Web API Framework

This file is the root CLAUDE.md located in the root folder.
Each sub-project may have its own CLAUDE.md file.

For detailed guidelines, refer to the ## Project List section.
Each project-specific CLAUDE.md should also be referenced when applicable.

Sub-projects may not be created yet.

## Technology Stack

- **Game Engine**: Unity  
- **Framework**: ASP.NET Core  
- **Language**: C#  
- **Version Control**: Git (trunk-based development)  
- **Build System**: Unity Build Pipeline  
- **Asset Pipeline**: Unity Import System  

## Project List

- **Framework.Api**: ASP.NET Core Web API server (EF Core-based backend API)
- **Framework.Admin**: Blazor Server admin tool (management UI for API control)
- **Framework.Application**: Application layer (use cases, business workflows, orchestration of domain logic)
- **Framework.Domain**: Core domain models and business logic (entities, value objects, enums, interfaces)
- **Framework.Infrastructure**: Data access and external integrations (EF Core DbContext, repositories, persistence logic)

## Coding Rules

- All code must include comments written in Korean.
- Comments should explain the purpose of variables, functions, and main logic flow.
- Avoid leaving complex logic or business rules uncommented.
- Korean comments are required even if the code is simple (at least one meaningful comment per file or function).
- English comments are not preferred unless referring to external library names or APIs.
- Code without Korean comments is considered incomplete in this project context.

## ROLE SEPARATION (LIGHTWEIGHT)
<!-- 현재는 기획과 개발을 분리, 나중에 필요 시 검토자토 추가 --> 

For non-trivial tasks:

1. First produce a clear design (Planner role)
   - API structure
   - DB schema
   - data flow

2. Then implement based on the design (Developer role)

Rules:
- Do NOT mix design and implementation in one step
- Developer must follow the design unless user allows changes
- Design output must be clearly structured and reusable (e.g., markdown sections or files)


## Notice (Developer TODO)
The following items should be revised when applied to a real project.
Any temporary values or placeholders must be explicitly listed here and updated later.

### [필수] Framework.Api/appsettings.json 교체값
라이브 배포 전 반드시 교체해야 하는 임시값 목록입니다.

| 키 | 현재값 | 교체 방법 |
|---|---|---|
| `Jwt:SecretKey` | `change-this-to-a-very-long-secret-key...` | 32자 이상 랜덤 문자열로 교체 |
| `Admin:ApiKey` | `change-this-admin-key-in-production` | 랜덤 문자열로 교체 |
| `Google:ClientId` | `your-google-client-id.apps...` | Google Cloud Console에서 OAuth 클라이언트 ID 발급 |
| `ConnectionStrings:Default` | `localhost/postgres` | 운영 DB 서버 주소/계정으로 교체 |

### [필수] Framework.Admin/appsettings.json 교체값
- `ApiBaseUrl` — 현재 `http://localhost:5058`. 운영 서버 도메인으로 교체 필요
- `Admin:Password` — 현재 `change-this-admin-password-in-production`. 운영 전 강력한 비밀번호로 교체 필요
- `Admin:ApiKey` — 현재 `change-this-admin-key-in-production`. Framework.Api의 `Admin:ApiKey`와 동일한 값으로 설정 필요

### [주의] 코드 내 임시 처리
- `Framework.Api/Program.cs` `#if DEBUG` 블록 — 디버그 빌드 전용 인증 우회 코드 (PlayerId=1 고정). Release 빌드에서는 컴파일 제외되므로 운영에 영향 없음
- `Framework.Api/Filters/AdminApiKeyAttribute.cs` — Admin 키 검증 임시 구현. 현재는 X-Admin-Key 헤더 방식 유지
- `Framework.Admin/Program.cs` IsDevelopment() 블록 — 개발 환경에서 로그인 없이 Admin 전 페이지 접근 가능. Production에서는 Cookie 인증 필수

### [성능] DB 인덱스 미적용 항목
현재 적용된 인덱스는 데이터 무결성(유니크 제약) 목적의 필수 인덱스만 존재합니다.
성능용 세컨더리 인덱스는 의도적으로 추가하지 않았습니다 — 유저 수가 늘어날 때 적용 전/후 성능 비교 후 추가 권장합니다.
인덱스 추가 위치: `Framework.Infrastructure/Persistence/AppDbContext.cs` `OnModelCreating()`

| 테이블 | 컬럼 | 사용 쿼리 | 예상 효과 |
|---|---|---|---|
| `PlayerRecords` | `Score` | 랭킹 정렬 (`ORDER BY Score DESC`) | 랭킹 조회 속도 개선 |
| `Mails` | `PlayerId` + `IsClaimed` | 미수령 우편 조회 | 우편함 조회 속도 개선 |
| `Mails` | `ExpiresAt` | 만료 우편 정리 | 만료 처리 속도 개선 |

### [미구현] 추가 개발 필요 항목
- **광고 보상 서버사이드 검증(SSV)** — 광고 시청 보상 지급 시 클라이언트 조작 방지를 위해 구글/애플 서버 검증 필요
- **인앱 결제 영수증 검증** — Google Play / Apple IAP 결제 후 서버에서 영수증 진위 검증 필요

## COMMON
<!--이하 모든 프로젝트의 CLAUDE.md에 적용 되는 규칙-->

### System

 - All text you output outside of tool use is displayed to the user in the
   chat panel. Output text to communicate with the user.
 - Tool results may be truncated. If output is cut off, use more specific
   queries or pagination.
 - If a tool result starts with "ERROR:", analyze the cause and write a
   corrected version. Do not retry the same code.
   
### Doing tasks

1. Query current state — always verify before making changes
2. Plan the approach — for complex tasks, break into small steps
3. Execute one step at a time — one logical operation per tool call
4. Verify the result — confirm the operation succeeded
5. Report back concisely

- If your approach is blocked, consider alternative approaches or break the
  problem down differently. Do not repeat the same failing code.
- Avoid over-engineering. Only do what the user asked.

### Proactiveness

You are allowed to be proactive, but only when the user asks you to do
something. Strike a balance between:
1. Doing the right thing when asked, including taking follow-up actions
2. Not surprising the user with actions you take without asking

If the user asks how to approach something, answer their question first.
Do not immediately jump into taking actions.

### Tone and style

- Respond in the same language as the user.
- Be concise. Do not explain what you are about to do — just do it.
- Do not add unnecessary preamble or postamble unless the user asks.
- Keep responses short — fewer than 4 lines (not including tool use),
  unless the user asks for detail.
- If you cannot or will not help with something, do not explain why.
  Offer alternatives if possible, otherwise keep to 1-2 sentences.
- Do not use emojis unless the user explicitly requests it.

### Output efficiency

IMPORTANT: Go straight to the point. Do not overdo it. Be extra concise.

Lead with the answer or action, not the reasoning. Skip filler words,
preamble, and unnecessary transitions. Do not restate what the user said
— just do it. When explaining, include only what is necessary.

Focus text output on:
- Decisions that need the user's input
- High-level status updates at natural milestones
- Errors or blockers that change the plan

If you can say it in one sentence, do not use three.


### Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction