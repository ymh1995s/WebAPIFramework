<!-- 이 파일은 /fullreview 스킬이 라운드 시작 시 자동 생성/갱신한다. 직접 편집 금지. -->
<!-- 직전 라운드 PLAN 스냅샷은 review/{ROUND_ID}/REVIEW_PLAN.snapshot.md 에 보존된다. -->

# WebAPIFramework 종합 리뷰 계획 — round_20260507

## 청크 목록

| 청크 | 에이전트 | 점검 ID | 산출물 |
|---|---|---|---|
| P1.1 의존성·책임·인터페이스 위치 | architect | A1, A2, A3 | p1_1_dependencies.md |
| P1.2 DTO·DI·트랜잭션·Content·캐시 | architect | A4, A5, A6, A7, AX-Cache | p1_2_dto_di_tx_content.md |
| P1.3 Strategy·인증·횡단·Currency-as-Item | architect | A8, A9, A10, A11 | p1_3_strategy_auth_xcut.md |
| P2.1 Controllers + 관측성 | qa-reviewer | Q1, Q2, Q3, Q4, Q5, Q10, AX-Observability | p2_1_controllers.md |
| P2.2 Services + 시간/타임존 | qa-reviewer | Q1, Q4, Q5, Q7, Q10, AX-Time | p2_2_services.md |
| P2.3 Repositories + Migrations + DbContext + 동시성 | qa-reviewer | Q6, Q8, AX-Migration, AX-Concurrency | p2_3_repos_migrations_dbcontext.md |
| P2.4 Razor + 테스트 커버리지 | qa-reviewer | Q9, AX-Test | p2_4_razor_tests.md |
| P3.1 인증·인가·디버그 | security-master | S1, S2, S3 | p3_1_authn_authz_debug.md |
| P3.2 입력·SQLi·IDOR·Rate | security-master | S4, S5, S6, S7 | p3_2_input_sqli_idor_rate.md |
| P3.3 시크릿·외부검증·OIDC·로깅·회복탄력성 | security-master | S8, S9, S10, S11, AX-Resilience | p3_3_secrets_external_oidc_log.md |
| P3.4 CORS·멱등·Admin·개인정보 | security-master | S12, S13, S14, S15 | p3_4_cors_idem_admin_pii.md |
| P3.5 도구 (gitleaks + 패키지 취약점) | security-master | S16, S17 | p3_5_tools.md |
| P4.1 최종 보고서 합본 | orchestrator | — | REVIEW_REPORT.md |

## 점검 ID 정의

| ID | 분류 | 설명 |
|---|---|---|
| A1 | 아키텍처 | 레이어 간 의존성 방향 (Domain ← Application ← Api/Admin, Domain ← Infrastructure) |
| A2 | 아키텍처 | 레이어별 책임 분리 (Controller는 조율만, Service는 도메인 로직만 등) |
| A3 | 아키텍처 | 인터페이스 위치 (Domain/Application Interfaces/) |
| A4 | 아키텍처 | DTO 정의 위치 및 레이어 경계 노출 범위 |
| A5 | 아키텍처 | DI 등록 일관성 및 수명 주기(Singleton/Scoped/Transient) |
| A6 | 아키텍처 | 트랜잭션 경계 및 IUnitOfWork 사용 일관성 |
| A7 | 아키텍처 | Content 영역 의존 방향 (Content → Framework, 역방향 금지) |
| A8 | 아키텍처 | Strategy 패턴 구현 일관성 (IAP/광고 검증기) |
| A9 | 아키텍처 | 인증/인가 흐름 설계 (JWT + X-Admin-Key 분리) |
| A10 | 아키텍처 | 횡단 관심사 등록 (예외핸들러·Rate Limit·점검모드·로깅) |
| A11 | 아키텍처 | Currency-as-Item 정합성 (Gold/Gems = ItemId 1/2) |
| AX-Cache | 아키텍처 | 캐시 Provider 설계 및 Admin 편집 시 캐시 갱신 흐름 |
| Q1 | 구현 | 한국어 주석 규칙 준수 |
| Q2 | 구현 | HTTP 응답 코드 정확성 |
| Q3 | 구현 | 모델 바인딩·유효성 검사 어트리뷰트 |
| Q4 | 구현 | 예외 처리 패턴 (도메인 예외 → ProblemDetails) |
| Q5 | 구현 | 비동기 패턴 (async/await, ConfigureAwait) |
| Q6 | 구현 | Repository 패턴 구현 일관성 |
| Q7 | 구현 | 시간/타임존 처리 (UTC vs KST, DateTimeOffset) |
| Q8 | 구현 | Migration 품질 (Down 메서드, idempotency) |
| Q9 | 구현 | Blazor 컴포넌트 안전성 (DirtyGuard, SafeErrorBoundary) |
| Q10 | 구현 | null 처리 및 방어 코드 |
| AX-Observability | 구현 | 로깅 구조화·상관관계 ID |
| AX-Time | 구현 | 시간 일관성 전수 점검 |
| AX-Migration | 구현 | xmin 마이그레이션 빈 Up/Down 처리 |
| AX-Concurrency | 구현 | 낙관적 동시성 토큰 매핑 전수 |
| AX-Test | 구현 | 테스트 프로젝트 커버리지 |
| S1 | 보안 | JWT 발급·검증·갱신 보안 |
| S2 | 보안 | 인가 체계 (Controller별 [Authorize]/[AdminApiKey]) |
| S3 | 보안 | 디버그 우회 코드 (#if DEBUG) |
| S4 | 보안 | 입력 유효성 검사 |
| S5 | 보안 | SQL 인젝션 |
| S6 | 보안 | IDOR (PlayerId 소유권 검증) |
| S7 | 보안 | Rate Limiting 정책 |
| S8 | 보안 | 시크릿 노출 |
| S9 | 보안 | 외부 서비스 검증 (Google/Unity Ads/IAP) |
| S10 | 보안 | OIDC/RTDN 인증 |
| S11 | 보안 | 로깅 내 민감정보 노출 |
| S12 | 보안 | CORS·HSTS·Swagger 노출 |
| S13 | 보안 | 멱등성 (이중 지급 방어) |
| S14 | 보안 | Admin 인가 (timing-safe 비교) |
| S15 | 보안 | 개인정보 처리 (PII 보관기간·탈퇴) |
| AX-Resilience | 보안 | 회복탄력성 (Polly/타임아웃/재시도) |
| S16 | 보안 | 시크릿 스캔 (gitleaks 또는 대체) |
| S17 | 보안 | 패키지 취약점 (dotnet list package --vulnerable) |
