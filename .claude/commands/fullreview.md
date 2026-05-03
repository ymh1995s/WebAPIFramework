# /fullreview — WebAPIFramework 컴팩트 무손실 풀리뷰 (Phase × Chunk)

WebAPIFramework 프로젝트를 **Phase × Chunk 2축 분할**로 전사 검토한다. 각 청크는 독립 에이전트 호출로 처리되고 산출물은 디스크에 박제되어, 컨텍스트 컴팩트가 발생해도 직전 청크 산출물만 있으면 다음 청크 재개가 가능하다.

---

## 사용법 (인자 규약)

| 인자 | 동작 |
|---|---|
| `/fullreview` | 신규 라운드 시작. 라운드 디렉토리/PLAN/STATUS 자동 생성 후 P1.1 → ... → P4.1 순차 실행 |
| `/fullreview 1` | Phase 1 전체 청크(P1.1, P1.2, P1.3) 순차 실행 |
| `/fullreview 1.2` | P1.2 청크 단일 실행 |
| `/fullreview resume` | `STATUS.md`의 첫 미완료 청크부터 재개 |
| `/fullreview report` | Phase 4 합본만 재실행 (REVIEW_REPORT.md 재생성) |
| `/fullreview status` | 현재 라운드 진행 상태 출력 |

`$ARGUMENTS`를 위 표 기준으로 파싱한다.

---

## 산출 디렉토리 정책

- 라운드 루트: `review/round_{YYYYMMDD}/`
- 현재 라운드 포인터: `review/CURRENT_ROUND.txt` — 1줄(라운드 폴더명만)
- 청크 산출물: `review/round_{YYYYMMDD}/p{N}_{chunk}_{slug}.md`
- 진행 추적: `review/round_{YYYYMMDD}/STATUS.md`
- PLAN 스냅샷: `review/round_{YYYYMMDD}/REVIEW_PLAN.snapshot.md`
- 직전 보고서 백업: `review/round_{YYYYMMDD}/REVIEW_REPORT.previous.md`
- 최종 보고서: 루트 `REVIEW_REPORT.md` 갱신 + 라운드 폴더 사본 저장
- `review/`는 `.gitignore` 등재됨

---

## 신규 라운드 초기화 절차 (`/fullreview` 인자 없음)

1. 오늘 날짜로 `ROUND_ID = round_{YYYYMMDD}` 결정. 같은 날 재시작 시 기존 폴더 재사용 여부 사용자 1회 확인 (기본: 재사용).
2. `review/{ROUND_ID}/` 생성.
3. 기존 루트 `REVIEW_REPORT.md`를 `review/{ROUND_ID}/REVIEW_REPORT.previous.md`로 백업(원본 유지).
4. 루트 `REVIEW_PLAN.md`를 본 스킬의 청크 카탈로그 기반으로 자동 작성(덮어쓰기) + 동일 본문을 라운드 스냅샷으로 복사.
5. `review/{ROUND_ID}/STATUS.md` 생성 — 모든 청크 `[ ] PENDING`으로 초기화.
6. `review/CURRENT_ROUND.txt`를 `{ROUND_ID}` 한 줄로 갱신.
7. P1.1부터 순차 실행.

---

## 재개 절차 (컴팩트 후 또는 `/fullreview resume`)

다음 파일만 읽으면 재개 가능 (in-memory 변수 사용 금지):

1. `CLAUDE.md`
2. `DEVNOTES.md`
3. `review/CURRENT_ROUND.txt` → `{ROUND_ID}`
4. `review/{ROUND_ID}/REVIEW_PLAN.snapshot.md`
5. `review/{ROUND_ID}/STATUS.md`
6. **직전 청크 산출물 1개** (다음 청크가 의존)
7. (Phase 4 재개 시) `review/{ROUND_ID}/p*.md` 글롭 전체

알고리즘:
- STATUS.md에서 `[ ]`/`[~]` 첫 청크 X 식별
- X의 직전 산출물을 PLAN/스킬 카탈로그로 조회 → 다음 청크 입력으로 전달
- X 청크 에이전트 호출

---

## 청크 카탈로그 (마스터 정의)

각 청크는 4요소를 가진다: **점검 ID 매핑 / 대상 파일·폴더 / 산출물 경로 / 인계 입력**.

### Phase 1 — architect (아키텍처 검토)

#### P1.1 의존성·책임·인터페이스 위치
- 점검 ID: A1, A2, A3
- 대상: 각 `.csproj` ProjectReference, `Framework.Domain/Interfaces/`, `Framework.Application/Interfaces/`, `Framework.Infrastructure/Repositories/`, `Framework.Application/Features/**/*Service.cs`(레이어 책임 측면), `Framework.Api/Controllers/**/*.cs`(레이어 책임 측면)
- 산출물: `review/{ROUND_ID}/p1_1_dependencies.md`
- 인계 입력: 없음(시작 청크)

#### P1.2 DTO·DI·트랜잭션·Content·캐시
- 점검 ID: A4, A5, A6, A7, AX-Cache
- 대상: `Framework.Api/Program.cs`, `Framework.Api/Extensions/ServiceExtensions.cs`, `Framework.Admin/Program.cs`, `Framework.Domain/Interfaces/IUnitOfWork.cs`, `Framework.Infrastructure/Persistence/UnitOfWork.cs`, `Framework.Application/Features/Reward/RewardDispatcher.cs`, `Framework.Domain/Content/`, `Framework.Application/Content/`, 캐시 Provider(`ILevelTableProvider`, `IItemMasterCache` 등) + Admin 마스터 편집 화면 → 캐시 갱신 흐름
- 산출물: `review/{ROUND_ID}/p1_2_dto_di_tx_content.md`
- 인계 입력: `p1_1_dependencies.md`

#### P1.3 Strategy·인증·횡단·Currency-as-Item
- 점검 ID: A8, A9, A10, A11(Currency-as-Item 정합성)
- 대상: `Framework.Api/Services/AdNetwork/`, `Framework.Api/Services/IapStore/`, JWT/X-Admin-Key 미들웨어·필터(`Framework.Api/Filters/`), Rate Limiting/점검 모드/전역 예외/로깅 등록부, `Framework.Domain/Entities/Item*`, `PlayerProfile`, `Mail`, `RewardDispatcher` 내 Currency 처리 경로
- 산출물: `review/{ROUND_ID}/p1_3_strategy_auth_xcut.md`
- 인계 입력: `p1_2_dto_di_tx_content.md`

### Phase 2 — qa-reviewer (구현 품질 검토)

#### P2.1 Controllers + 관측성
- 점검 ID: Q1, Q2, Q3, Q4, Q5, Q10(Controller 범위), AX-Observability
- 대상: `Framework.Api/Controllers/**/*.cs` 전수, `Framework.Api/ProblemDetails/`, `Framework.Api/Middleware/`(로깅/예외)
- 산출물: `review/{ROUND_ID}/p2_1_controllers.md`
- 인계 입력: `p1_3_strategy_auth_xcut.md`

#### P2.2 Services + 시간/타임존
- 점검 ID: Q1, Q4, Q5, Q7, Q10(Service 범위), AX-Time
- 대상: `Framework.Application/Features/**/*Service.cs` 전수, `RewardDispatcher`, `DailyLogin*Service`, `MailService`, `AuthService`, 시간 사용 지점 전반(UtcNow vs DateTimeOffset, KST 변환)
- 산출물: `review/{ROUND_ID}/p2_2_services.md`
- 인계 입력: `p2_1_controllers.md`

#### P2.3 Repositories + Migrations + DbContext + 동시성
- 점검 ID: Q6, Q8, AX-Migration, AX-Concurrency
- 대상: `Framework.Infrastructure/Repositories/*.cs` 전수, `Framework.Infrastructure/Migrations/*.cs` 전수(스키마+데이터 이전+Down 메서드+idempotency), `Framework.Infrastructure/Persistence/AppDbContext.cs`, 엔티티 동시성 토큰 매핑부([ConcurrencyCheck]/xmin)
- 산출물: `review/{ROUND_ID}/p2_3_repos_migrations_dbcontext.md`
- 인계 입력: `p2_2_services.md`

#### P2.4 Razor + 테스트 커버리지
- 점검 ID: Q9, AX-Test
- 대상: `Framework.Admin/Components/Pages/**/*.razor.cs` 전수, `DirtyGuardBase`/`SafeComponentBase`/`SafeErrorBoundary` 사용처, 솔루션 내 `*Test*`/`*Tests*` 프로젝트 존재 여부
- 산출물: `review/{ROUND_ID}/p2_4_razor_tests.md`
- 인계 입력: `p2_3_repos_migrations_dbcontext.md`

### Phase 3 — security-master (보안 검토)

#### P3.1 인증·인가·디버그
- 점검 ID: S1, S2, S3
- 대상: `Framework.Api/Services/JwtTokenProvider.cs`, `Framework.Application/Features/Auth/AuthService.cs`, `Framework.Api/Filters/AdminApiKeyAttribute.cs`, `RequireLinkedAccountAttribute.cs`, 모든 Controller `[Authorize]`/`[AdminApiKey]` 적용 현황, `Framework.Api/Program.cs`+`Framework.Admin/Program.cs`의 `#if DEBUG`
- 산출물: `review/{ROUND_ID}/p3_1_authn_authz_debug.md`
- 인계 입력: `p2_4_razor_tests.md`

#### P3.2 입력·SQLi·IDOR·Rate
- 점검 ID: S4, S5, S6, S7
- 대상: 모든 DTO Annotation, Controller 파라미터 범위, `FromSqlRaw`/`ExecuteSqlRaw` 검색, PlayerId 자원 접근 본인 확인 경로, Rate Limiting 정책 등록부
- 산출물: `review/{ROUND_ID}/p3_2_input_sqli_idor_rate.md`
- 인계 입력: `p3_1_authn_authz_debug.md`

#### P3.3 시크릿·외부검증·OIDC·로깅·회복탄력성
- 점검 ID: S8, S9, S10, S11, AX-Resilience
- 대상: `Framework.Api/appsettings*.json`, `Framework.Admin/appsettings*.json`, `.gitignore`, `Framework.Api/Services/GoogleTokenVerifier.cs`, `UnityAdsVerifier.cs`, `IronSourceVerifier.cs`, `IapStore/GooglePlayStoreVerifier.cs`, `GooglePubSubAuthenticator.cs`, 로거 사용처, `IHttpClientFactory` 등록 + Polly/타임아웃 정책
- 산출물: `review/{ROUND_ID}/p3_3_secrets_external_oidc_log.md`
- 인계 입력: `p3_2_input_sqli_idor_rate.md`

#### P3.4 CORS·멱등·Admin·개인정보
- 점검 ID: S12, S13, S14, S15
- 대상: CORS·HSTS·Swagger 등록부, `RewardGrants`/`RewardDispatcher` 동시성 처리, `AdminApiKeyAttribute` 비교 로직(timing-safe), 계정 탈퇴 흐름 + FK CASCADE 매핑
- 산출물: `review/{ROUND_ID}/p3_4_cors_idem_admin_pii.md`
- 인계 입력: `p3_3_secrets_external_oidc_log.md`

#### P3.5 도구 (gitleaks + 패키지 취약점)
- 점검 ID: S16, S17
- 대상: 리포 전체. 도구 부재 시 대체 절차 적용
- 산출물: `review/{ROUND_ID}/p3_5_tools.md`
- 인계 입력: `p3_4_cors_idem_admin_pii.md`

### Phase 4 — orchestrator (합본)

#### P4.1 최종 보고서 합본
- 입력: `review/{ROUND_ID}/p*.md` 글롭 전체
- 산출물: 루트 `REVIEW_REPORT.md` 덮어쓰기 + `review/{ROUND_ID}/REVIEW_REPORT.md` 사본
- 에이전트: orchestrator 직접 처리(별도 에이전트 미호출). 컴팩트 시 `/fullreview report`로 재실행

---

## 청크별 산출물 표준 형식

각 `p{N}_{chunk}_{slug}.md`는 다음 섹션을 반드시 포함:

- 메타: ROUND_ID / 에이전트 / 점검 ID / 입력 인계 경로
- 점검 항목별 결과: 항목별 PASS/WARN/FAIL + 위반(파일:라인 — 심각도 — 설명) + 근거
- 식별된 이슈 표: 심각도/파일·라인/점검 ID/설명/권고 조치 컬럼
- 인계 노트: 검토 완료 모듈 / 핫스팟 / 의문점·미해결 질문
- **다음 청크 입력 요약 (200단어 이내)** — 다음 청크 프롬프트에 그대로 붙여넣을 압축본

마지막 섹션이 핵심: 다음 청크는 이 200단어 + 점검 ID + STATUS만으로도 정상 동작해야 함.

---

## STATUS.md 형식

| 청크 | 상태 | 산출물 | 시작 | 완료 |
|---|---|---|---|---|

상태 코드: `[ ] PENDING` / `[~] IN_PROGRESS` / `[x] DONE` / `[!] FAILED`.

마지막에 "최근 인계 노트 요약" 섹션으로 직전 완료 청크의 200단어 요약을 보존.

---

## 청크 실행 표준 절차 (모든 청크 공통)

1. STATUS.md의 해당 청크 행을 `[~] IN_PROGRESS` + 시작 시각으로 갱신
2. 직전 청크 산출물 경로를 카탈로그에서 조회 → 읽기
3. 해당 Phase 에이전트(architect/qa-reviewer/security-master)를 **백그라운드**로 호출. 프롬프트에 다음 포함:
   - 청크 코드 + 점검 ID + 합격 기준
   - 대상 파일·폴더 목록
   - 직전 청크 200단어 요약
   - 산출물 경로(쓰기 권한): `review/{ROUND_ID}/p{N}_{chunk}_{slug}.md`
   - 산출물 표준 형식 준수 의무
   - **코드 수정 절대 금지** (산출물 .md만 쓰기 허용)
4. 에이전트 종료 후 산출물 파일 존재 검증 → STATUS.md를 `[x] DONE` + 완료 시각 + 200단어 요약 갱신
5. 다음 청크 진행. 단일 청크 인자였다면 종료
6. 청크 실패 시 `[!] FAILED` + 사유 1줄 기록, 사용자에게 보고. 자동 재시도 금지

---

## Phase 4 합본 절차

1. `review/{ROUND_ID}/p*.md` 글롭 전체 수집
2. 각 산출물 "식별된 이슈 표"를 심각도별 통합
3. 다음 구조로 루트 `REVIEW_REPORT.md` 작성(덮어쓰기 전 사용자 1회 확인):
   - Executive Summary (라운드 정보, 청크 완료 현황, 심각도별 합계, Top 5 즉시 조치)
   - 1장 Phase 1 결과 (1.1/1.2/1.3)
   - 2장 Phase 2 결과 (2.1/2.2/2.3/2.4)
   - 3장 Phase 3 결과 (3.1/3.2/3.3/3.4/3.5)
   - 4장 Critical Issues
   - 5장 High Issues
   - 6장 Medium Issues
   - 7장 Low / 추적 항목
   - 8장 DEVNOTES.md 갱신 권고
   - 부록 A. 청크 산출물 인덱스
4. 동일 본문 `review/{ROUND_ID}/REVIEW_REPORT.md`에 사본 저장
5. STATUS.md P4.1을 `[x] DONE`으로 마감

---

## 보안 도구(P3.5) 대체 절차

- gitleaks 부재 시: `appsettings*.json`, `.env*`, `secrets/` 전반에 `(?i)(secret|apikey|api_key|password|token|jwt)` 패턴 grep 대체
- `dotnet list package --vulnerable` 미지원 환경: 대체 도구(`dotnet-outdated`) 부재 명시 + NuGet 패키지 수동 점검 권고를 산출물에 기록

---

## 주의사항

- 모든 에이전트는 **읽기 전용** + 산출물 `.md` 1개 쓰기만 허용
- 에이전트 실행은 `run_in_background: true` (programmer 미사용 — 리뷰 전용)
- in-memory 변수 사용 금지. 모든 인계는 산출물 파일을 통해
- 중간 청크 완료는 사용자에게 보고하지 않음. 단 **Phase 4 합본 직전 확인** + **최종 보고**는 필수
- 컴팩트 발생 시 `/fullreview resume`만으로 마지막 미완료 청크부터 재개 가능해야 함 — 본 스킬의 핵심 설계 목표
