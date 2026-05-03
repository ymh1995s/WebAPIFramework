# WebAPIFramework 종합 리뷰 보고서 (round_20260503)

> 라운드: 2026-05-03 | 청크 분할 13개(검토 12 + 합본 1) 모두 완료
> 직전 라운드(2026-04-30) 보고서: `review/round_20260503/REVIEW_REPORT.previous.md`
> 청크 산출물 인덱스: 부록 A 참고

---

## Executive Summary

### 라운드 메타
- ROUND_ID: round_20260503
- 검토 청크: P1.1~P1.3 / P2.1~P2.4 / P3.1~P3.5 (12)
- 에이전트: architect 3 + qa-reviewer 4 + security-master 5
- 총 식별 이슈: **101건** (Critical 2 / High 14 / Med 51 / Low 34)

### 심각도별 합계
| 심각도 | 건수 | 즉시 조치 |
|---|---|---|
| Critical | 2 | 2 |
| High | 14 | 14 |
| Med | 51 | 0 |
| Low | 34 | 0 |

### Top 5 즉시 조치 (Critical → 비용 대비 영향 순)

| # | ID | 위치 | 영향 |
|---|---|---|---|
| 1 | C-1 | Framework.Admin/Program.cs:67 + Routes.razor + 전체 Razor 페이지 | 로그인 없이 모든 Admin 페이지/데이터 노출·변경 가능 |
| 2 | C-2 | Framework.Api/Extensions/ServiceExtensions.cs:371 | 단일 공격자로 전체 인증 API 차단(전체 가용성 결함) |
| 3 | H-15 | .gitignore + appsettings.Development.json | 평문 JWT/DB Password/Admin ApiKey/Google ClientId git 노출 |
| 4 | H-9 | Framework.Admin/Program.cs:125 | /admin-login Rate Limit 부재 → 분산 비밀번호 추측 공격 |
| 5 | H-12 | AuthService.cs:180 + AppDbContext.cs:404 | IAP 이력 보유 플레이어 탈퇴 시 FK 위반으로 GDPR 삭제 실패 |

---

## 1장. 아키텍처 검토 결과 (Phase 1)

### 1.1 의존성·책임·인터페이스 (P1.1)

| 점검 ID | 결과 |
|---|---|
| A1 의존성 방향 | WARN — 그래프는 준수, 단 Application이 EFCore 패키지 직접 참조(Med) |
| A2 레이어 책임 | **FAIL** — Admin 컨트롤러 2곳(AppDbContext 직접 주입, High×2), Application Service 7곳 DbUpdateException catch(Med), IapRtdnController 페이로드 디코딩(Med) |
| A3 인터페이스 위치 | PASS |

### 1.2 DTO·DI·트랜잭션·Content·캐시 (P1.2)

| 점검 ID | 결과 |
|---|---|
| A4 DTO 경계 | WARN — Player 영역 17건 익명 객체 응답 잔존(IapPurchase/IapRtdn/AdsCallback 핵심) |
| A5 DI 등록 일관성 | WARN — RateLimitLogRepository 인터페이스 없이 구체 등록(Med), IUnitOfWork/BanLog 그룹화 결여 |
| A6 트랜잭션 경계 | WARN — 다단계 쓰기 9개 Service UoW 적용 PASS, BeginTransactionAsync 재진입 가드 부재(Low), AdminPlayerService Ban/Unban 명시 UoW 미사용(Low), 3개 UNIQUE catch DetachEntry 누락(Low) |
| A7 Content 영역 분리 | PASS (grep 위반 0건, 컴파일러 강제력 없음) |
| AX-Cache | WARN — Level/SystemConfig 캐시 정합, LevelTableProvider Singleton 동기 블로킹(Med), IItemMasterCache 미구현(Med) |

### 1.3 Strategy·인증·횡단·Currency-as-Item (P1.3)

| 점검 ID | 결과 |
|---|---|
| A8 Strategy 패턴 | PASS — AdNetwork/IapStore Resolver 정합. GooglePlayClientFactory 인터페이스 부재(Med), IapConsumerResolver 패턴 불일치(Low) |
| A9 인증 분리 | PASS — JWT/AdminKey 스킴 독립, Admin 23/23 + Player 8/12(의도된 익명 4) |
| A10 횡단관심사 | WARN — VersionController Rate Limit 누락(Med), MatchMakingHub 메서드 throttle 부재(Med), GlobalExceptionHandler ProblemDetails 미사용·환경 분기 정책 불일치(Med), 점검 모드 인라인 람다(Med) |
| A11 Currency-as-Item | PASS — Gold/Gems 컬럼 제거 마이그레이션 완전, RewardDispatcher/MailService 신규 경로 정합. Mail.ItemId/Item deprecated 잔존(Med), MailService.SendAsync 다중 정책 미적용(Med), CurrencyIds 시드 보호 가드 부재(Low) |

---

## 2장. 구현 품질 검토 결과 (Phase 2)

### 2.1 Controllers + 관측성 (P2.1)

| 점검 ID | 결과 |
|---|---|
| Q1 한국어 주석 | PASS (34파일 전수) |
| Q2 네이밍 | PASS |
| Q3 DTO 사용 | **FAIL** — Player 27건 + Admin 3건 = 30건 익명 객체 응답 잔존 |
| Q4 Async 일관성 | PASS — .Result/.Wait() 0건. CancellationToken 전파 전무(Low) |
| Q5 예외 처리 | WARN — AdsCallback/IapPurchase catch-all로 GlobalExceptionHandler 우회(Med×2) |
| Q10 Magic String | WARN — Rate Limit 정책명 5종 문자열 산재(Med) |
| AX-Observability | WARN — **UseSerilogRequestLogging 미호출(High)**, Enrichment 미설정(Med), AuthController ILogger 미주입(Low), traceId 전체 미포함 |

### 2.2 Services + 시간/타임존 (P2.2)

| 점검 ID | 결과 |
|---|---|
| Q1 한국어 주석 | PASS (22 Service 전수) |
| Q4 Async | WARN — LevelTableProvider .GetAwaiter().GetResult() 동기 블로킹(Med) |
| Q5 예외 처리 | WARN — RewardDispatcher Direct 경로 Exp 가산 시 레벨업 누락(Med), AuthService ILogger 미주입(Med), MailService 감사 로그 트랜잭션 격리 미흡(Low) |
| Q7 멱등성 | WARN — DailyLoginService RewardDispatcher 미경유(DEVNOTES 명세 불일치, Med) |
| Q10 Magic String | WARN — IsUniqueViolation 5곳 중복(Med), BuildBundleAsync 2곳 동일 코드 중복(Med), dedupKey 네이밍 불일치(Med), SourceKey 패턴 산재(Med) |
| AX-Time | PASS — DateTime.UtcNow 일관, KST 변환 정확, DST 영향 없음 |

### 2.3 Repos + Migrations + DbContext + 동시성 (P2.3)

| 점검 ID | 결과 |
|---|---|
| Q6 EF Core 패턴 | PASS — N+1 방지, DB 페이지네이션 적용. Admin 전용 GetAllAsync 3곳 미적용(Med), 플레이어 우편 전체 로딩(Med) |
| Q8 Soft Delete | PASS — Player Global Query Filter + 나머지 수동 필터 일관 |
| AX-Migration | PASS — 30개 마이그레이션 전수 정합, Currency-as-Item 4단계 정합. 수동 재실행 시 수량 누적 주의(Med) |
| AX-Concurrency | **FAIL** — **PlayerItem.Quantity 동시성 토큰 미적용(High)** — Currency-as-Item Lost Update 위험. IapPurchase.Status 토큰 미적용(Med) |

### 2.4 Razor + 테스트 커버리지 (P2.4)

| 점검 ID | 결과 |
|---|---|
| Q9 Razor 일관성 | PASS(조건부) — 21개 업무 페이지 중 18개 SafeComponentBase/DirtyGuardBase 일관. 미적용 3페이지(InquiryTest/MatchMaking/AdminNotifications) Med. RewardDispatch.razor.cs CS8604 경고(Med) |
| AX-Test | **FAIL** — 테스트 프로젝트 0개, .sln 부재, 핵심 비즈니스 로직 자동화 검증 전무(High) |

---

## 3장. 보안 검토 결과 (Phase 3)

### 3.1 인증·인가·디버그 (P3.1)

| 점검 ID | 결과 |
|---|---|
| S1 JWT 흐름 | PASS(조건부) — 검증 파라미터·HS256·OnMessageReceived 양호. RefreshToken 보안 메타데이터(IP/UA/Revoked) 부재(Med), DB 평문 저장(Med), JWT SecretKey 길이 가드 부재(High) |
| S2 인가 누락 | **FAIL Critical** — Framework.Admin AddAuthorization() FallbackPolicy 미설정 + 모든 Razor 페이지 [Authorize] 미부착 → 익명 사용자가 모든 운영 페이지 직접 URL 접근 가능, X-Admin-Key 자동 부착으로 데이터 변경까지 가능 |
| S3 디버그 우회 | PASS — #if DEBUG 블록 Release 컴파일 제외 정상 |

### 3.2 입력·SQLi·IDOR·Rate (P3.2)

| 점검 ID | 결과 |
|---|---|
| S4 입력 검증 | PASS(조건부) — 핫패스(Auth/Inquiry/IAP) 검증 양호. Mail/Shout/Notice/Item/AdminGrantReward DTO 길이·범위 검증 부재(Med), JoinMatchRequestDto enum 검증 부재(Med), 페이지네이션 클램프 미일관(Med) |
| S5 SQL Injection | **PASS** — 솔루션 전체 raw SQL/SqlCommand 사용 0건, LINQ 파라미터 바인딩 일관 |
| S6 IDOR | **PASS** — MailService.ClaimAsync PlayerId 강제 비교, 모든 Player 컨트롤러 JWT PlayerId만 신뢰 |
| S7 Rate Limiting | **FAIL Critical** — auth 정책이 AddFixedWindowLimiter 단일 글로벌(파티션 키 없음). 모든 IP/유저 합산 분당 60회 → 단일 공격자로 전체 인증 차단 |

### 3.3 시크릿·외부검증·OIDC·로깅·회복탄력성 (P3.3)

| 점검 ID | 결과 |
|---|---|
| S8 시크릿 관리 | PASS(조건부) — .gitignore/secrets/docker-compose 정상. appsettings.Development.json 평문 시크릿 git 추적(Med, P3.5에서 High로 격상), JWT 길이 가드 부재(Med), Iap PackageName/RtdnAudience docker-compose 매핑 부재(Med) |
| S9 외부 검증 | PASS(조건부) — Google IdToken Audience/HMAC FixedTimeEquals/UNIQUE token/PackageName 검증 모두 양호 |
| S10 OIDC RTDN | PASS(조건부) — JWKS 동적 회전 + Issuer/Audience/Lifetime/서명 4종 검증. messageId dedup 비즈니스 멱등성 의존(추적) |
| S11 로깅 민감정보 | **FAIL Med** — IapRtdnService L82,L141 PurchaseToken 평문 로깅(MaskToken 미사용). DeviceId/IdToken/Refresh/Access/ServiceAccountJson 평문 로깅 0건 |
| AX-Resilience | **FAIL High** — DB EnableRetryOnFailure 미설정, HealthChecks 0건, Polly/Microsoft.Extensions.Http.Resilience 미참조 → 일시 장애 시 즉시 5xx |

### 3.4 CORS·멱등·Admin·개인정보 (P3.4)

| 점검 ID | 결과 |
|---|---|
| S12 CORS/보안 헤더/Swagger | **FAIL High** — API/Admin 모두 X-Content-Type-Options/X-Frame-Options/Referrer-Policy/CSP 0건. CORS는 모바일 단독 구조라 Informational. Swagger Development 분기 OK |
| S13 보상 멱등 | **PASS** — RewardDispatcher UNIQUE 선기록 + DbUpdateException catch 패턴이 AdReward/IAP/StageClear/LevelUp/AdminGrant 전 경로 통일. DailyLogin/MailClaim 동등 보장 |
| S14 Admin 보호 | **FAIL High** — /admin-login Rate Limit 미적용(P3.2 재확인). Cookie HttpOnly/Secure/SameSite 명시 부재(Med). FixedTimeEquals + BCrypt(workFactor=12) 양호 |
| S15 개인정보 | **FAIL High** — (1) IapPurchase.PlayerId Restrict ↔ AuthService.WithdrawAsync hard delete → IAP 이력 보유자 탈퇴 시 FK 위반, (2) RefreshToken DB 평문 저장(P3.3 미해결), (3) AuditLog/RateLimitLog/IapPurchase.ClientIp 보관 정책 0건 |

### 3.5 도구 (P3.5)

| 점검 ID | 결과 |
|---|---|
| S16 시크릿 하드코딩 | **FAIL High** — appsettings.Development.json이 .gitignore 미등재로 git 추적. 평문 노출: JWT SecretKey, DB Password "postgres", Admin ApiKey "admin", BCrypt PasswordHash, 실 Google OAuth ClientId. C# 소스/PEM/Bearer/외부 SaaS 키 0건 |
| S17 취약 패키지 | Informational — dotnet list package --vulnerable 도구 미실행(샌드박스). 직접 의존성 14종 모두 .NET 10 GA(10.0.6) + 최신 안정. 사용자 로컬에서 재실행 권고 |

---

## 4장. Critical Issues (즉시 조치)

### C-1. Framework.Admin 모든 Razor 페이지 인가 누락
- **파일**: `Framework.Admin/Program.cs:67` + `Components/Routes.razor` + 운영 Razor 페이지 27개 전부
- **영향**: 익명 사용자가 `/players`, `/admin-notifications`, `/reward-dispatch` 등 모든 Admin 페이지 직접 URL 접근. ApiHttpClient의 X-Admin-Key 자동 부착으로 데이터 조회/변경 가능
- **해결**: `AddAuthorization(opts => opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())` 또는 `_Imports.razor`에 `@attribute [Authorize]` 일괄 적용 + Login/Error/NotFound만 `[AllowAnonymous]`

### C-2. `auth` Rate Limit 파티션 키 부재 — 인증 API 전체 가용성 결함
- **파일**: `Framework.Api/Extensions/ServiceExtensions.cs:371-377`
- **현상**: `AddFixedWindowLimiter("auth", ...)` 오버로드는 단일 limiter 인스턴스 생성(파티션 함수 없음). AuthController 전체에 `[EnableRateLimiting("auth")]` 적용되어 모든 IP·유저 합산 분당 60회. 단일 공격자가 정상 사용자 인증을 차단 가능
- **해결**: `AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(ipKey, ...))`로 IP 파티션 적용. PermitLimit IP당 분당 10~20

---

## 5장. High Issues (다음 스프린트)

| ID | 항목 | 파일 | Phase |
|---|---|---|---|
| H-1 | Admin 컨트롤러 2곳 AppDbContext 직접 주입 + EF 쿼리 인라인 | AdminRateLimitLogsController.cs:14, AdminSecurityController.cs:14 | P1.1 |
| H-2 | UseSerilogRequestLogging 미호출 — HTTP 요청 자동 구조화 로그 미활성 | Framework.Api/Program.cs | P2.1 |
| H-3 | PlayerItem.Quantity 동시성 토큰 미적용 — Currency-as-Item Lost Update | PlayerItem.cs:14 + AppDbContext.cs:218 | P2.3 |
| H-4 | 테스트 프로젝트 0개 — RewardDispatcher/Auth/IAP 자동화 검증 전무 | 솔루션 전체 | P2.4 |
| H-5 | JWT SecretKey 길이 가드 부재 — 32자 미만 키 주입 시 사전 차단 없음 | JwtTokenProvider.cs:24 | P3.1 |
| H-6 | RefreshToken DB 평문 저장 + 보안 메타데이터(IP/UA/Revoked) 부재 | RefreshToken.cs:13 | P3.1 / P3.4 |
| H-7 | DB EnableRetryOnFailure 미설정 — 일시 단절 시 즉시 5xx | Framework.Api/Program.cs:41 | P3.3 |
| H-8 | HealthChecks 미등록 — /health 엔드포인트 부재, probe 불가 | Framework.Api/Program.cs | P3.3 |
| H-9 | Polly/Microsoft.Extensions.Http.Resilience 미참조 — 외부 호출 retry/timeout 부재 | Framework.Api.csproj | P3.3 |
| H-10 | API/Admin 보안 응답 헤더 0건(X-Content-Type-Options/X-Frame-Options/Referrer-Policy/CSP) | Program.cs (양쪽) | P3.4 |
| H-11 | /admin-login Rate Limit 부재 + Antiforgery Disable — 분산 비밀번호 추측 차단 불가 | Framework.Admin/Program.cs:125 | P3.1 / P3.2 / P3.4 |
| H-12 | IapPurchase.PlayerId Restrict ↔ Withdraw hard delete FK 충돌 — IAP 이력 보유자 탈퇴 실패 | AuthService.cs:180 + AppDbContext.cs:404 | P3.4 |
| H-13 | (중복 — H-6과 동일 항목 — RefreshToken 평문) | RefreshToken.cs:13 | P3.4 |
| H-14 | appsettings.Development.json git 추적 — 평문 JWT SecretKey/DB Password/Admin ApiKey/Google ClientId 노출 | .gitignore + appsettings.Development.json | P3.3 / P3.5 |
| H-15 | .gitignore에 `**/appsettings.Development.json` 추가 + git rm --cached + 시크릿 회전 | (단일 한 줄 수정 + 즉시 차단) | P3.5 |

---

## 6장. Medium Issues (백로그)

### Phase 1 — 아키텍처

| ID | 항목 | 파일 |
|---|---|---|
| M-1 | Application의 Microsoft.EntityFrameworkCore 패키지 직접 참조 | Framework.Application.csproj:10 |
| M-2 | Application Service 7곳 DbUpdateException catch — UNIQUE/동시성 처리 누수 | AdPolicy/IapProduct/IapPurchase/RewardTable/RewardDispatcher/DailyLogin/Mail |
| M-3 | IapRtdnController 페이로드 디코딩/PackageName 검증 인라인 | IapRtdnController.cs:43-110 |
| M-4 | Player 영역 17건 익명 객체 응답 잔존(IapPurchase 7, IapRtdn 7, AdsCallback 6) | Controllers/Player |
| M-5 | RateLimitLogRepository 인터페이스 없이 구체 클래스 등록 | ServiceExtensions.cs:347 |
| M-6 | LevelTableProvider Singleton 동기 블로킹(.GetAwaiter().GetResult()) | LevelTableProvider.cs:65 |
| M-7 | IItemMasterCache 미구현 — 핫패스 매 요청 DB 조회 | ItemMasterService 전반 |
| M-8 | GooglePlayClientFactory 인터페이스 부재 — Strategy 구현체가 구체 어댑터 결합 | GooglePlayClientFactory.cs |
| M-9 | Mail.ItemId/Item deprecated 단일 경로 잔존 — 디케이 일정 부재 | Mail.cs:15-16, 35-36 |
| M-10 | MailService.SendAsync 다중 정책 미적용 — 단일 아이템 경로만 | MailService.cs:57-72 |
| M-11 | VersionController Rate Limit 미부착 | VersionController.cs:10 |
| M-12 | MatchMakingHub 메서드 throttle 부재(SignalR 메시지 단위 무방어) | MatchMakingHub.cs |
| M-13 | GlobalExceptionHandler ProblemDetails 미사용 + EnumHandler 정책 불일치 | GlobalExceptionHandler.cs |
| M-14 | 점검 모드 인라인 람다 — 응집도/예외 안전성 부족 | Program.cs:130-154 |

### Phase 2 — 구현 품질

| ID | 항목 | 파일 |
|---|---|---|
| M-15 | RewardDispatcher Direct 경로 Exp 가산 시 ExpService 미경유 — 레벨업 판정 누락 | RewardDispatcher.cs:186-193 |
| M-16 | AuthService ILogger 미주입 — 인증 핵심 경로 보안 감사 추적 불가 | AuthService.cs |
| M-17 | DailyLoginService RewardDispatcher 미경유 — RewardGrants 기록 없음(DEVNOTES 명세 불일치) | DailyLoginService.cs:118 |
| M-18 | IsUniqueViolation 메서드 5곳 동일 로직 중복 + 1곳 괄호 누락 | RewardDispatcher/IapPurchase/AdPolicy/IapProduct/RewardTable |
| M-19 | BuildBundleAsync 2곳 완전 동일 코드 중복(IAP + AdReward) | IapPurchaseService.cs:263 + AdRewardService.cs:146 |
| M-20 | AdminNotification dedupKey 접두사 네이밍 불일치(하이픈/언더스코어 혼용) | IapPurchaseService/IapRtdnService |
| M-21 | SourceKey 패턴 문자열 산재(7가지 패턴) — 상수화 부재 | 5+ Service |
| M-22 | AdsCallback/IapPurchase catch-all로 GlobalExceptionHandler 우회 | AdsCallbackController/IapPurchaseController |
| M-23 | Rate Limit 정책명 5종 문자열 리터럴 산재 — 오타 silent fail | ServiceExtensions + 8개 Controller |
| M-24 | Serilog Enrichment 미적용(Enrich.FromLogContext 등) — 요청 단위 PlayerId/TraceId 부재 | Framework.Api/Program.cs |
| M-25 | RewardDispatch.razor.cs CS8604 — UsedMode null 가능 | RewardDispatch.razor.cs:175 |
| M-26 | Razor 미적용 3페이지 SafeComponentBase 미상속 | InquiryTest/MatchMaking/AdminNotifications |
| M-27 | Admin 전용 GetAllAsync 3곳 페이지네이션 미적용 | NoticeRepository/InquiryRepository/RewardTableRepository |
| M-28 | MailRepository.GetByPlayerIdAsync 페이지네이션 부재 — 장기 플레이어 수천 건 가능 | MailRepository.cs:20 |
| M-29 | IapPurchase.Status 동시성 토큰 미적용 — verify 중 RTDN 충돌 가능 | IapPurchase.cs:31 |
| M-30 | Currency-as-Item 데이터 이전 SQL 수동 재실행 시 수량 누적 위험 | 20260501100332_AddCurrencyAsItem.cs |

### Phase 3 — 보안

| ID | 항목 | 파일 |
|---|---|---|
| M-31 | RefreshToken 보안 메타데이터(IpAddress/UserAgent/RotatedFromId/RevokedAt) 부재 | RefreshToken.cs |
| M-32 | TokenValidationParameters에 ValidAlgorithms 미설정, ClockSkew 명시 없음 | ServiceExtensions.cs:321 |
| M-33 | AccessToken 만료 1시간 — JTI blacklist 부재로 도난 시 최대 1시간 노출 | JwtTokenProvider.cs:40 |
| M-34 | 빌드 모드 가드 부재 — Debug 바이너리 운영 배포 시 디버그 우회 활성화 | Program.cs (양쪽) |
| M-35 | JoinMatchRequestDto Tier/HumanType enum 검증 부재 | MatchDto.cs:6 |
| M-36 | Mail/Shout/Notice/Item/AdminGrantReward DTO 길이·범위 검증 부재 | 다수 DTO |
| M-37 | Admin 컨트롤러 일부 페이지네이션 클램프 부재 — 거대 pageSize 입력 시 DoS 가능 | AdminShoutsController 외 |
| M-38 | options.GlobalLimiter 미설정 — 미커버 엔드포인트 무제한(VersionController 등) | ServiceExtensions.cs |
| M-39 | JWT SecretKey 길이 가드 부재 | JwtTokenProvider.cs:24 |
| M-40 | Iap PackageName/RtdnAudience docker-compose 매핑 부재 — 운영 배포 시 잘못된 값 위험 | appsettings.json:38,40 |
| M-41 | IapRtdnService PurchaseToken 평문 로깅(MaskToken 미사용) | IapRtdnService.cs:81-83, 140-142 |
| M-42 | RTDN messageId dedup 캐시 부재 — 비즈니스 멱등성 의존 | IapRtdnController.cs:41 |
| M-43 | Admin Cookie 옵션 HttpOnly/Secure/SameSite 명시 부재 | Framework.Admin/Program.cs:59 |
| M-44 | AuditLog/RateLimitLog/BanLog/AdminNotification PII 보관기간 정책 부재 | 다수 엔티티 |
| M-45 | IapPurchase.ClientIp 보관 정책 부재 | IapPurchase.cs:58 |
| M-46 | UNIQUE 위반 판별이 메시지 문자열 매칭(Contains "23505") — Npgsql 버전/언어팩 의존 | 5개 Service |
| M-47 | appsettings.Development.json 평문 시크릿 git 추적 (P3.3에서 식별, P3.5 High로 격상) | (H-14/H-15 참조) |
| M-48 | HttpClient timeout 명시 부재 — Google 라이브러리 기본값 의존 | GooglePlayClientFactory |
| M-49 | AdminGrantRewardDto.SourceKey 길이/형식 미검증 | AdminRewardDispatchController.cs:81 |
| M-50 | IapVerifyRequest ProductId/PurchaseToken MaxLength 부재 | IapVerifyRequest.cs |
| M-51 | Admin BCrypt PasswordHash git 노출(workFactor=12로 보호되나 약한 평문 시 사전공격 가능) | appsettings.Development.json:9 |

---

## 7장. Low / 추적 항목

| ID | 항목 |
|---|---|
| L-1 | Application의 Microsoft.Extensions.Caching.Memory 직접 참조 |
| L-2 | AdminRewardDispatchController DTO 3개 Controller 파일 내 인라인 정의 |
| L-3 | DTO를 Controller 파일 내 인라인 정의(SecurityTimelineItemDto) |
| L-4 | DailyLoginController/MatchMakingController/StagesController 익명 객체 응답 잔존 |
| L-5 | IUnitOfWork.BeginTransactionAsync 재진입 가드 없이 인터페이스 노출 |
| L-6 | AdminPlayerService Ban/Unban 명시 UoW 미사용(원자성 OK·일관성 미흡) |
| L-7 | UNIQUE catch에 DetachEntry 누락 3곳 |
| L-8 | IUnitOfWork/BanLog 등록 그룹화 일관성 부족 |
| L-9 | IMatchMakingService Singleton 등록 — Scoped Repo 결합 시 캡처드 디펜던시 위험 |
| L-10 | OnRejected 콜백에서 Repository 직접 사용(Service 미경유) |
| L-11 | LevelTableProvider 분산 환경 stale 위험 |
| L-12 | IapConsumerResolver Dictionary 매핑 미적용(다른 Resolver와 패턴 불일치) |
| L-13 | RequireLinkedAccount 응답 익명 객체 |
| L-14 | RewardDispatcher 괄호 누락 1건(동작 동일하나 비일관) |
| L-15 | KstOffset 상수 2곳 중복 |
| L-16 | 광고 일일 한도 UTC/KST 기준 불일치 |
| L-17 | RefundReason "Voided"/"Canceled" 문자열 리터럴 |
| L-18 | MailService Reason "MailClaim" 하드코딩 |
| L-19 | PlayerRepository.BanAsync/UnbanAsync 도메인 행위를 Repository에서 수행 |
| L-20 | CurrencyIds 시드 보호 가드 부재(ItemId=1/2 수정/삭제 차단 없음) |
| L-21 | CancellationToken 전체 미전파 |
| L-22 | AsNoTracking 전역 미사용 다수 |
| L-23 | RateLimitLogRepository 인터페이스 미정의 |
| L-24 | RefreshTokenRepository.DeleteAllByPlayerIdAsync 지연 실행 |
| L-25 | 14개 Razor 페이지 SafeComponentBase 상속하나 SafeExecute 미사용 |
| L-26 | LevelThresholds 페이지 다수 행 편집인데 DirtyGuardBase 미적용 |
| L-27 | MainLayout.razor CS0162 / MatchMaking CS0649 — 의도된 빌드 분기 경고 |
| L-28 | VersionController [AllowAnonymous] 명시 부재 |
| L-29 | Login.razor SSR 폼(HTTPS는 운영에서 강제) |
| L-30 | UnityAds/IronSource ts 누락 fallback(HMAC가 ts 포함하므로 간접 검증) |
| L-31 | OIDC 검증 실패에도 200 응답(Pub/Sub 재시도 방지 의도) |
| L-32 | AuthController ex.Message 응답 직접 노출(현재 사용자 메시지뿐, 향후 위험) |
| L-33 | Serilog PII 마스킹 enricher 미적용 |
| L-34 | Admin 키 회전 시 프로세스 재시작 필요(Singleton + 시작 시 인코딩) |

---

## 8장. DEVNOTES.md 갱신 권고

### 신규 [Caution] 섹션 추가
- Framework.Admin 인가 정책: FallbackPolicy 또는 `_Imports.razor` `@attribute [Authorize]` 일괄 적용 확정 시 절차 박제
- `appsettings.Development.json` 정책: `**/appsettings.Development.json` .gitignore 등재 + dev 시크릿 User Secrets 또는 .env로 이전 절차 명문화

### 기존 명세와 코드 불일치 정정
- "DailyLoginLog + RewardGrants 이중보호 유지" → 실제 코드에 RewardGrants 미기록. 문구 수정 또는 RewardDispatcher 경유 전환

### [기술 부채] 섹션 보강
- PlayerItem.Quantity 동시성 토큰 미적용 — Currency-as-Item Lost Update 위험. ExecuteUpdate 패턴 또는 IsConcurrencyToken 도입
- IapPurchase 탈퇴 FK Restrict ↔ Withdraw hard delete 충돌 — Withdraw를 SoftDelete + PII 익명화로 전환 권고
- RefreshToken DB 평문 저장 — SHA-256 해시 저장 마이그레이션 계획
- AuditLog/RateLimitLog/IapPurchase.ClientIp 보관기간 정책 — 90/180일 BackgroundService 정리 작업 신설
- 테스트 프로젝트 0개 — Framework.Tests xUnit 신설 P0 권고(RewardDispatcher + ClaimAsync 통합 테스트)
- DB EnableRetryOnFailure / HealthChecks / Polly Resilience — 운영 배포 전 필수 도입
- 보안 응답 헤더 미들웨어 — API/Admin 양쪽 일괄 도입

### [설계 결정] 명문화 후보
- Mail.ItemId/Item deprecated 단일 경로 디케이 일정(예: 모든 미수령 우편 소진 후 컬럼 Drop)
- `auth` Rate Limit IP 파티션 구조 + 한도 정책
- Item 마스터 Singleton 캐시 도입 시 Update/Delete 시 Invalidate 흐름

---

## 부록 A. 청크 산출물 인덱스

| 청크 | 산출물 |
|---|---|
| P1.1 의존성·책임·인터페이스 | review/round_20260503/p1_1_dependencies.md |
| P1.2 DTO·DI·트랜잭션·Content·캐시 | review/round_20260503/p1_2_dto_di_tx_content.md |
| P1.3 Strategy·인증·횡단·Currency-as-Item | review/round_20260503/p1_3_strategy_auth_xcut.md |
| P2.1 Controllers + 관측성 | review/round_20260503/p2_1_controllers.md |
| P2.2 Services + 시간/타임존 | review/round_20260503/p2_2_services.md |
| P2.3 Repos+Migrations+DbContext+동시성 | review/round_20260503/p2_3_repos_migrations_dbcontext.md |
| P2.4 Razor + 테스트 커버리지 | review/round_20260503/p2_4_razor_tests.md |
| P3.1 인증·인가·디버그 | review/round_20260503/p3_1_authn_authz_debug.md |
| P3.2 입력·SQLi·IDOR·Rate | review/round_20260503/p3_2_input_sqli_idor_rate.md |
| P3.3 시크릿·외부검증·OIDC·로깅·회복탄력성 | review/round_20260503/p3_3_secrets_external_oidc_log.md |
| P3.4 CORS·멱등·Admin·개인정보 | review/round_20260503/p3_4_cors_idem_admin_pii.md |
| P3.5 도구 | review/round_20260503/p3_5_tools.md |
| 직전 라운드 보고서(2026-04-30) | review/round_20260503/REVIEW_REPORT.previous.md |
| PLAN 스냅샷 | review/round_20260503/REVIEW_PLAN.snapshot.md |
| 진행 상태 | review/round_20260503/STATUS.md |
