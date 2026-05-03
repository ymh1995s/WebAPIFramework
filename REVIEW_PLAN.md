<!-- 이 파일은 /fullreview 스킬이 라운드 시작 시 자동 생성/갱신한다. 직접 편집 금지. -->
<!-- 직전 라운드 PLAN 스냅샷은 review/{ROUND_ID}/REVIEW_PLAN.snapshot.md 에 보존된다. -->

# WebAPIFramework 종합 리뷰 계획 — round_20260503

> 라운드 시작: 2026-05-03
> 본 PLAN은 `/fullreview` 스킬이 청크 카탈로그 기반으로 자동 작성. 진행 추적은 `review/round_20260503/STATUS.md` 참조.

## 라운드 메타

- ROUND_ID: `round_20260503`
- 라운드 디렉토리: `review/round_20260503/`
- 직전 보고서 백업: `review/round_20260503/REVIEW_REPORT.previous.md`
- 현재 라운드 포인터: `review/CURRENT_ROUND.txt`

## 청크 카탈로그 (12청크 + 합본 1청크 = 13)

| 청크 | 에이전트 | 점검 ID | 산출물 경로 |
|---|---|---|---|
| P1.1 의존성·책임·인터페이스 | architect | A1, A2, A3 | `review/round_20260503/p1_1_dependencies.md` |
| P1.2 DTO·DI·트랜잭션·Content·캐시 | architect | A4, A5, A6, A7, AX-Cache | `review/round_20260503/p1_2_dto_di_tx_content.md` |
| P1.3 Strategy·인증·횡단·Currency-as-Item | architect | A8, A9, A10, A11 | `review/round_20260503/p1_3_strategy_auth_xcut.md` |
| P2.1 Controllers + 관측성 | qa-reviewer | Q1~Q5, Q10, AX-Observability | `review/round_20260503/p2_1_controllers.md` |
| P2.2 Services + 시간/타임존 | qa-reviewer | Q1, Q4, Q5, Q7, Q10, AX-Time | `review/round_20260503/p2_2_services.md` |
| P2.3 Repos+Migrations+DbContext+동시성 | qa-reviewer | Q6, Q8, AX-Migration, AX-Concurrency | `review/round_20260503/p2_3_repos_migrations_dbcontext.md` |
| P2.4 Razor + 테스트 커버리지 | qa-reviewer | Q9, AX-Test | `review/round_20260503/p2_4_razor_tests.md` |
| P3.1 인증·인가·디버그 | security-master | S1, S2, S3 | `review/round_20260503/p3_1_authn_authz_debug.md` |
| P3.2 입력·SQLi·IDOR·Rate | security-master | S4, S5, S6, S7 | `review/round_20260503/p3_2_input_sqli_idor_rate.md` |
| P3.3 시크릿·외부검증·OIDC·로깅·회복탄력성 | security-master | S8, S9, S10, S11, AX-Resilience | `review/round_20260503/p3_3_secrets_external_oidc_log.md` |
| P3.4 CORS·멱등·Admin·개인정보 | security-master | S12, S13, S14, S15 | `review/round_20260503/p3_4_cors_idem_admin_pii.md` |
| P3.5 도구 (gitleaks + 패키지 취약점) | security-master | S16, S17 | `review/round_20260503/p3_5_tools.md` |
| P4.1 합본 | orchestrator | — | 루트 `REVIEW_REPORT.md` (+ 라운드 사본) |

## 점검 항목 합격 기준

### Phase 1 (architect) — A 시리즈

| ID | 항목 | 합격 기준 |
|---|---|---|
| A1 | 의존성 방향 | 각 .csproj ProjectReference 전수. Domain이 상위 레이어 미참조, Application이 Infrastructure 미참조 |
| A2 | 레이어 책임 | Controller에 비즈니스 로직 없음, Service에 EF Core 직접 호출 없음, Repository에 도메인 규칙 없음 |
| A3 | 인터페이스 위치 | Repository/외부연동 인터페이스는 Domain 또는 Application, 구현은 Infrastructure |
| A4 | DTO 경계 | Domain 엔티티가 Controller 응답으로 그대로 노출되지 않음 |
| A5 | DI 등록 일관성 | Program.cs와 ServiceExtensions 등록 책임 분리, 누락 없음 |
| A6 | 트랜잭션 경계 | IUnitOfWork 사용 패턴 일관성, 중첩 BeginTransaction 재진입 방지 |
| A7 | Content 영역 분리 | Framework 영역이 Content 영역을 참조하지 않음 |
| A8 | Strategy 패턴 | AdNetwork/IapStore Resolver 패턴 일관성 |
| A9 | 인증 분리 | JWT(Player)와 X-Admin-Key(Admin) 독립성, 충돌 없음 |
| A10 | 횡단관심사 | Rate Limiting / 점검 모드 / 전역 예외 / 로깅 적용 일관성 |
| A11 | Currency-as-Item 정합성 | cef8a87 이후 Gold/Gems가 PlayerItem(ItemId=1/2)로 완전 이전, 잔존 컬럼/참조/마이그레이션 누락 없음 |
| AX-Cache | 캐시 invalidation | Singleton 캐시(`ILevelTableProvider`/`IItemMasterCache` 등)가 Admin 마스터 변경 시 갱신됨 |

### Phase 2 (qa-reviewer) — Q 시리즈

| ID | 항목 | 합격 기준 |
|---|---|---|
| Q1 | 한국어 주석 | 파일/함수당 최소 1개 의미 있는 한국어 주석 (CLAUDE.md 강제) |
| Q2 | 네이밍 | C# 표준 + 프로젝트 컨벤션(I접두/Async접미/DTO·Service·Repository 접미) |
| Q3 | DTO 사용 | 모든 Controller 응답이 DTO |
| Q4 | Async 일관성 | async/await 누락, .Result/.Wait() 사용, CancellationToken 전파 |
| Q5 | 예외 처리 | GlobalExceptionHandler 통합 흐름 준수 |
| Q6 | EF Core 패턴 | N+1, AsNoTracking 누락, Migration 스키마+데이터 이전 전사 |
| Q7 | 멱등성 | RewardGrants 선기록, 중복 호출 방어 흐름 |
| Q8 | Soft Delete | IsDeleted 쿼리 일관 적용 |
| Q9 | Razor 컴포넌트 | DirtyGuardBase / SafeComponentBase / SafeErrorBoundary 일관 사용 |
| Q10 | Magic String | enum/상수화 미흡 영역 |
| AX-Observability | 관측성 | 핵심 경로 구조화 로그/상관 ID, ProblemDetails traceId |
| AX-Time | 시간/타임존 | UtcNow vs DateTimeOffset 일관성, KST 변환 정확성(daily reward) |
| AX-Migration | 마이그레이션 운영 안전성 | Down 메서드, 데이터 이전 idempotency, EnsureCreated/Migrate 혼용 여부 |
| AX-Concurrency | 동시성 토큰 | [ConcurrencyCheck]/xmin 적용 범위(PlayerItem/Mail/IapPurchase) |
| AX-Test | 테스트 커버리지 | 테스트 프로젝트 존재 여부, 핵심 경로 단위·통합 테스트 |

### Phase 3 (security-master) — S 시리즈

| ID | 항목 |
|---|---|
| S1 | JWT 흐름 — SecretKey 길이, AccessToken 유효기간, RefreshToken 회전 |
| S2 | 인가 — [Authorize]/[AdminApiKey]/[RequireLinkedAccount] 누락 엔드포인트 |
| S3 | 디버그 우회 — `#if DEBUG` 블록 Release 제외 재확인 |
| S4 | 입력 검증 — DTO ModelState/Annotation, 파라미터 범위 |
| S5 | SQL Injection — LINQ/파라미터화, FromSqlRaw/ExecuteSqlRaw |
| S6 | IDOR — PlayerId 자원 접근 본인 확인 |
| S7 | Rate Limiting — 추가 보호 필요 엔드포인트 |
| S8 | 시크릿 관리 — appsettings 평문 시크릿, .gitignore |
| S9 | 외부 검증 — Google IdToken / Unity Ads HMAC / IronSource HMAC / Google Play Receipt / RTDN OIDC |
| S10 | OIDC RTDN — JWT 검증, Audience, replay 방지 |
| S11 | 로깅 민감정보 — JWT/Token/Receipt/DeviceId 평문 로깅 |
| S12 | CORS / 보안 헤더 — 허용 Origin, HSTS, Swagger 운영 노출 |
| S13 | 보상 멱등 — 동시 클레임 ConcurrencyToken, 중복 지급 방어 |
| S14 | Admin 보호 — X-Admin-Key 타이밍 공격, 갱신 절차 |
| S15 | 개인정보 — 계정 탈퇴 CASCADE 범위, 보관기간 |
| S16 | gitleaks/grep — appsettings/.env 시크릿 하드코딩 이력 |
| S17 | dotnet list package --vulnerable — 취약 패키지 |
| AX-Resilience | 회복탄력성 — 외부 호출 타임아웃·재시도, HttpClient 수명관리 |

## 진행 상태

`review/round_20260503/STATUS.md` 참조.
