# WebAPIFramework 종합 리뷰 보고서 (round_20260507)

> 라운드: 2026-05-08 | 청크 분할 13개(검토 12 + 합본 1) 모두 완료
> 직전 라운드(round_20260503) 보고서: `review/round_20260507/REVIEW_REPORT.previous.md`
> 청크 산출물 인덱스: 부록 A 참고

---

## Executive Summary

### 라운드 메타
- ROUND_ID: round_20260507
- 검토 청크: P1.1~P1.3 / P2.1~P2.4 / P3.1~P3.5 (12)
- 에이전트: architect 3 + qa-reviewer 4 + security-master 5
- 총 식별 이슈: **74건** (Critical 0 / High 2 / Med 35 / Low 37)
- **주요 신규 발견**: docker-compose 인프라 격차 2건(광고 SecretKey 누락, secrets/ 볼륨 마운트 누락), GooglePlayClientFactory 매 호출 디스크 I/O
- **이전 라운드 누락 보완**: round_20260503 미해결 상태였으나 round_20260507 에이전트가 재확인 안 한 항목 10건 코드 직접 확인 후 추가 (M-26~M-35)

### 심각도별 합계

| 심각도 | 건수 |
|---|---|
| Critical | 0 |
| High | 2 |
| Medium | 35 |
| Low | 37 |

### Top 5 즉시 조치

| # | ID | 위치 | 영향 |
|---|---|---|---|
| 1 | H-1 | `docker-compose.yml:38-51` | UnityAds/IronSource SecretKey 매핑 누락 → 운영 시 광고 SSV 전면 장애 |
| 2 | H-2 | `Framework.Api/Services/IapStore/GooglePlayClientFactory.cs:33-49` | 매 IAP 호출마다 디스크 I/O + JSON 파싱 → 결제 검증 신뢰성 취약 |
| 3 | M-22 | `docker-compose.yml` | `secrets/` 볼륨 마운트 누락 → IAP 검증 전면 장애 위험 |
| 4 | M-4 | `Framework.Application/Features/Auth/AuthDto.cs` | Auth DTO 4종 `[Required]`/`[MaxLength]` 미부착 → 빈 문자열 서비스 도달 |
| 5 | M-10 | 테스트 프로젝트 전체 | RewardDispatcher/MailService 단위 테스트 완전 부재 → 핵심 보상 파이프라인 미검증 |

---

## 1장. 아키텍처 검토 결과 (Phase 1)

### 1.1 의존성·책임·인터페이스 (P1.1)

| 점검 ID | 결과 |
|---|---|
| A1 의존성 방향 | PASS — 5개 `.csproj` 역방향 0건. Domain.Common 신규 모듈 위치 정합 |
| A2 레이어 책임 | WARN — Controller 비즈니스 로직 0건, Repository 영속성 한정 OK. 단 `PiiRetentionCleanupService`가 Application에서 EF Core `DbContext` Service Locator 패턴 사용(이 때문에 Application.csproj에 EF Core + Npgsql 패키지 잔류) |
| A3 인터페이스 위치 | PASS — Domain/Interfaces 22개 위치 정상, Infrastructure 인터페이스 0건 |

**신규 발견 — Domain.Common 모듈 채택 현황**:
- `DomainException`: 6곳 채택 진행 중 (양호)
- `Result<T>/Error`: 사용처 0건 (dead code 위험)
- `Guard`: 사용처 0건 (dead code 위험)
- 기존 6종 결과 타입(record 4 + enum 2) 형식 불일치 공존

### 1.2 DTO·DI·트랜잭션·Content·캐시 (P1.2)

| 점검 ID | 결과 |
|---|---|
| A4 DTO 위치 | PASS — Entity 직접 노출 0건, Vertical Slice 일관 |
| A5 DI 수명 주기 | PASS — Singleton 8종 모두 안전, Captive Dependency 0건 |
| A6 트랜잭션 | PASS — IUnitOfWork.ExecuteInTransactionAsync 10곳 일관, BeginTransaction 직접 호출 0건 |
| A7 Content 의존 방향 | WARN — `PlayerWithdrawalCleaner`가 Content 테이블(`StageClears`) 직접 정리(OCP 위반). `SourceKeys.cs`에 Stage 메서드 3종 비-Content 위치 |
| AX-Cache | PASS — LevelTableProvider 1시간 TTL + Admin 편집 시 Invalidate 트리거 정상 |

### 1.3 Strategy·인증·횡단·Currency-as-Item (P1.3)

| 점검 ID | 결과 |
|---|---|
| A8 Strategy 패턴 | PASS — AdNetwork/IapStore Verifier/Consumer 3종 동일 골격, 확장성 정상 |
| A9 인증/인가 흐름 | WARN — AuthDomainException 7종/핸들러 순서 정상. 단 `PlayerBannedException` 응답 코드 GuestLogin(403) vs 나머지(401) 불일치 |
| A10 횡단 관심사 | PASS — 예외 핸들러 4단계(EnumDeserialization→AuthDomain→Domain→Global), Rate Limit 5정책, 점검 미들웨어 위치 정상 |
| A11 Currency-as-Item | PASS — Gold/Gems 컬럼 제거 완료, ItemType.Currency 단일 판별, RewardDispatcher 통합 경로 |

---

## 2장. 구현 품질 검토 결과 (Phase 2)

### 2.1 Controllers + 관측성 (P2.1)

| 점검 ID | 결과 |
|---|---|
| Q1 한국어 주석 | PASS |
| Q2 HTTP 응답 코드 | WARN — MailsController.Claim: bool 반환으로 404/400 미구분 |
| Q3 모델 바인딩 | WARN — Auth DTO 3종(RefreshToken/GoogleLogin/LinkGoogle) `[Required]` 미부착 |
| Q4 예외 처리 | WARN — AuthController `InvalidOperationException` 범용 catch 2건, ProblemDetails 미준수 |
| Q5 비동기 | PASS — async void / .Result / .Wait() 0건 |
| Q10 null 처리 | PASS |
| AX-Observability | WARN — TraceId 전파 정상, 비즈니스 이벤트 로깅 대부분 누락(Low) |

### 2.2 Services + 시간/타임존 (P2.2)

| 점검 ID | 결과 |
|---|---|
| Q1 한국어 주석 | PASS — Service 28 + BackgroundService 4파일 전수 |
| Q4 예외 처리 | PASS — 예외 삼킴 0건 |
| Q5 비동기 | PASS |
| Q7 시간/타임존 | PASS — Application 레이어 `DateTime.Now` 0건, KST TimeConstants 일관 |
| Q10 null 처리 | WARN — DailyRewardSlot 미설정 시 사일런트 스킵 |
| AX-Time | PASS |

**추가 발견**: `PiiRetentionCleanupService` 품질 양호(KST 스케줄/청크/HealthCheck 연동 정상). DEVNOTES.md "HealthCheck 통합 미적용" 항목은 **이미 구현 완료** — DEVNOTES 갱신 필요.

### 2.3 Repositories + Migrations + DbContext + 동시성 (P2.3)

| 점검 ID | 결과 |
|---|---|
| Q6 Repository 패턴 | PASS — 자율 SaveChanges 0건, IUnitOfWork 일관 사용 |
| Q8 Migration 품질 | PASS — 30개 전수 Down() 완전 역전. 2건 Medium |
| AX-Migration xmin | PASS — Mail/PlayerItem/IapPurchase 3건 빈 본문 처리 완료 |
| AX-Concurrency | PASS — 동시성 토큰 매핑(PlayerItem/Mail/IapPurchase) 정상 |

### 2.4 Razor + 테스트 커버리지 (P2.4)

| 점검 ID | 결과 |
|---|---|
| Q9 컴포넌트 안전성 | WARN — SafeErrorBoundary 전역 적용 확인, SafeComponentBase 상속률 78%(21/27). RewardTables/LevelThresholds DirtyGuardBase 미적용 |
| AX-Test | WARN — 47 테스트 전통과. RewardDispatcher/MailService 단위 테스트 완전 부재 |

---

## 3장. 보안 검토 결과 (Phase 3)

### 3.1 인증·인가·디버그 (P3.1)

| 점검 ID | 결과 |
|---|---|
| S1 JWT 보안 | WARN — RefreshToken 해시 저장 PASS. AccessToken 1시간 하드코딩(Medium), RotatedFromId 부재(Medium), ClockSkew 기본 5분(Low), HS256(Low) |
| S2 인가 체계 | PASS — Player 17 + Admin 23 컨트롤러 전수. 공개 엔드포인트 모두 의도적 |
| S3 디버그 우회 | PASS — `#if DEBUG` Release 제외 확인 |

### 3.2 입력·SQLi·IDOR·Rate (P3.2)

| 점검 ID | 결과 |
|---|---|
| S4 입력 유효성 | WARN — Auth DTO 4종 `[Required]`/`[MaxLength]` 미부착(P2.1 중복) |
| S5 SQL 인젝션 | PASS — Raw SQL 0건 |
| S6 IDOR | PASS — JWT PlayerId만 사용, 소유권 가드 확인 |
| S7 Rate Limiting | PASS — 5정책 + GlobalLimiter 600/분 |

### 3.3 시크릿·외부검증·OIDC·로깅·회복탄력성 (P3.3)

| 점검 ID | 결과 |
|---|---|
| S8 시크릿 노출 | WARN — **High: docker-compose.yml 광고 SecretKey 매핑 누락**. Dev appsettings 더미값 평문(Medium) |
| S9 외부 서비스 검증 | PASS — 모든 검증기 서명 검증 수행, 우회 경로 0건 |
| S10 OIDC/RTDN | PASS — 4종 검증 정상, JWKS 캐시 Singleton |
| S11 로깅 민감정보 | PASS — 토큰/DeviceId 직접 로깅 0건 |
| AX-Resilience | WARN — **High: GooglePlayClientFactory 매 호출 디스크 I/O**. JWKS 타임아웃 없음(Medium), Polly 미도입(Medium) |

### 3.4 CORS·멱등·Admin·개인정보 (P3.4)

| 점검 ID | 결과 |
|---|---|
| S12 CORS·HSTS·Swagger | PASS — CORS 의도적 미등록, HSTS Production 한정, Swagger Development 한정 |
| S13 멱등성 | PASS — RewardGrants UNIQUE + IsUniqueViolation catch, WithdrawAsync 멱등 |
| S14 Admin 인가 | PASS — FixedTimeEquals, 22개 Admin 컨트롤러 [AdminApiKey] 전수. Medium: docker-compose Admin 서비스 정의 부재, /admin-login DisableAntiforgery |
| S15 PII | PASS — WithdrawAnonymize/IapPurchase Restrict FK/PiiRetention 4단계 정상. Medium: secrets/ 볼륨 마운트 누락, RewardGrant FK OnDelete 명시 누락 |

### 3.5 도구 (P3.5)

| 점검 ID | 결과 |
|---|---|
| S16 시크릿 스캔 | WARN — gitleaks 미설치(대체 grep 수행). 운영 시크릿 git 추적 0건. CI 자동화 미통합(Medium) |
| S17 패키지 취약점 | PASS — 28개 패키지 .NET 10 GA 안정 라인. 알려진 CVE 매칭 0건. CI 자동 스캔 미통합(Medium) |

---

## 4장. Critical Issues

없음.

---

## 5장. High Issues

### H-1 docker-compose.yml UnityAds/IronSource SecretKey 매핑 누락
- **위치**: `docker-compose.yml:38-51`
- **점검 ID**: S8
- **설명**: `AdNetworks__UnityAds__SecretKey`, `AdNetworks__IronSource__SecretKey` 환경변수가 docker-compose에 정의되지 않음. `.env.example`에도 항목 부재. 운영 배포 시 SecretKey가 appsettings 플레이스홀더 값으로 동작 → 모든 광고 SSV 서명 검증 실패 → 광고 보상 파이프라인 전면 장애.
- **권고**: `.env.example`에 `UNITY_ADS_SECRET_KEY`, `IRONSOURCE_SECRET_KEY` 추가 + `docker-compose.yml`에 `AdNetworks__UnityAds__SecretKey: ${UNITY_ADS_SECRET_KEY}`, `AdNetworks__IronSource__SecretKey: ${IRONSOURCE_SECRET_KEY}` 매핑 추가.

### H-2 GooglePlayClientFactory 매 호출 디스크 I/O + JSON 파싱
- **위치**: `Framework.Api/Services/IapStore/GooglePlayClientFactory.cs:33-49`
- **점검 ID**: AX-Resilience
- **설명**: IAP 검증/소비 요청마다 서비스 계정 JSON 파일을 디스크에서 읽고 `JsonDocument.Parse` 수행. 디스크 장애 또는 파일 락 경합 시 결제 검증 전체 차단. 부하 시 불필요한 I/O 반복.
- **권고**: `ServiceAccountCredential`을 Singleton 또는 `Lazy<>` 패턴으로 캐싱. `GooglePlayClientFactory` 자체를 Singleton DI 등록. 초기화 실패는 시작 시점에 조기 감지.

---

## 6장. Medium Issues

| ID | 위치 | 점검 ID | 설명 | 권고 조치 |
|---|---|---|---|---|
| M-1 | `Framework.Application/BackgroundServices/PiiRetentionCleanupService.cs:127` | A2 | Application 레이어가 EF Core DbContext Service Locator 사용 — Application.csproj에 EF Core + Npgsql 패키지 잔류 | 옵션A `IPiiRetentionRepository` Domain 신설/Infrastructure 구현, 옵션B BackgroundService Infrastructure 이동 |
| M-2 | `Framework.Application.csproj:10,17` | A1/A2 | Application이 EF Core + Npgsql PackageReference 보유(M-1의 근본 원인) | M-1 해결 시 자동 제거 가능 |
| M-3 | `Framework.Application/Features/*` (6종 결과 타입) | A2 | GrantRewardResult 등 6종 결과 타입 형식 불일치, Result&lt;T&gt; 미채택 | 신규 기능부터 `Result<T>/Error` 적용, ADR 작성 |
| M-4 | `Framework.Infrastructure/Repositories/PlayerWithdrawalCleaner.cs:72-74` | A7 | Framework 영역이 Content 테이블(StageClears) 직접 정리 — OCP 위반 | `IPlayerDataPurger` Strategy 인터페이스 도입, Content별 Purger 별도 등록 |
| M-5 | `Framework.Api/Middleware/AuthDomainExceptionHandler.cs:37` | A9 | PlayerBannedException GuestLogin(403) vs 핸들러(401) 응답 코드 불일치 | 핸들러에 `is PlayerBannedException → 403` 분기 추가 또는 통일 정책 명문화 |
| M-6 | `Framework.Api/Controllers/Player/MailsController.cs:39` | Q2 | Claim 실패 시 400 일괄 반환 — 존재하지 않는 우편은 404가 적절 | MailService.ClaimAsync 반환 타입을 Result/enum으로 변경하여 상황 구분 |
| M-7 | `Framework.Application/Features/Auth/AuthDto.cs:18,21,24,47` | Q3/S4 | Auth DTO 4종(RefreshToken/GoogleLogin/LinkGoogle/ResolveConflict) `[Required]`/`[MaxLength]` 미부착 | `[Required] [MaxLength(4096)]` 일괄 부착 |
| M-8 | `Framework.Api/Controllers/Player/AuthController.cs:118,142` | Q4 | InvalidOperationException 범용 catch 2건 — 비의도 예외가 409/400으로 반환될 수 있음 | 전용 도메인 예외(AccountAlreadyLinkedException 등) + ProblemDetails 규격 응답 |
| M-9 | `Framework.Application/Features/DailyLogin/` | Q10 | DailyRewardSlot 미설정 시 사일런트 스킵 — 운영 오류 감지 부재 | `slot is null` 시 `LogWarning` 추가 |
| M-10 | 테스트 프로젝트 전체 | AX-Test | RewardDispatcher 단위 테스트 완전 부재 — 핵심 보상 파이프라인(멱등성/Direct-Mail 분기/레벨업 체인) 미검증 | 최소 5개 시나리오(정상Direct/정상Mail/중복SourceKey/빈번들/레벨업체인) 신설 |
| M-11 | 테스트 프로젝트 전체 | AX-Test | MailService 단위 테스트 완전 부재 — ClaimAsync 동시성 재시도/감사 로그 미검증 | ClaimAsync 단위 테스트 클래스 신설 |
| M-12 | `Framework.Admin/Components/Pages/Admin/RewardTables.razor.cs` | Q9 | 일괄 Entries 편집 시 DirtyGuardBase 미적용 — 이탈 경고 없음 | DirtyGuardBase 상속으로 전환 |
| M-13 | `Framework.Admin/Components/Pages/Admin/LevelThresholds.razor.cs` | Q9 | 전체 테이블 편집 시 DirtyGuardBase 미적용 | DirtyGuardBase 상속으로 전환 |
| M-14 | `Framework.Api/Services/JwtTokenProvider.cs:40` | S1 | AccessToken 만료 1시간 하드코딩 — 탈취 시 노출 시간 길고 옵션화 부재 | `Jwt:AccessTokenMinutes` 옵션 추가, 기본 15~30분으로 단축 |
| M-15 | `Framework.Domain/Entities/RefreshToken.cs` | S1 | RotatedFromId 부재 — 도난 토큰 패밀리 단위 폐기 불가 | 회전 추적 필드 추가 + Replay 탐지 시 패밀리 일괄 Revoke (별도 라운드 권장) |
| M-16 | `Framework.Admin/appsettings.Development.json:9` | S8 | Admin BCrypt 해시 git 커밋 — Production 재사용 시 즉시 노출 | Production PasswordHash는 환경변수(`Admin__PasswordHash`)로 주입, Dev 재사용 금지 가이드 명시 |
| M-17 | `Framework.Api/appsettings.Development.json:9-18` | S8 | Dev 환경 DB 비밀번호/Admin Key/JWT SecretKey 평문 + 실제 Google ClientId 노출 | Dev/Prod OAuth 클라이언트 분리, `.env.example` 안내값 그대로 사용 금지 명시 |
| M-18 | `Framework.Api/Services/IapStore/GooglePubSubAuthenticator.cs:56` | AX-Resilience | JWKS 갱신 시 GetConfigurationAsync 타임아웃 없음 | 5~10초 CancellationToken 전달 |
| M-19 | `Framework.Admin/Program.cs:90-95` | AX-Resilience | Admin → API 호출 HttpClient에 Polly 재시도/서킷브레이커 미적용 | `AddTransientHttpErrorPolicy` 추가 검토 |
| M-20 | `Framework.Admin/Program.cs:148` | S14 | /admin-login `DisableAntiforgery()` — CSRF 표준 위반(실위험 낮음) | Antiforgery 토큰 발급 + `RequireAntiforgery()` 적용 |
| M-21 | `docker-compose.yml` | S14 | docker-compose.yml에 Admin 서비스 정의 자체 없음 — 운영 IaC 격차 | admin 서비스 추가(환경변수 주입 포함) |
| M-22 | `docker-compose.yml:51` | S15 | `secrets/` 디렉터리 볼륨 마운트 누락 — 컨테이너 시작 시 GooglePlayClientFactory 초기화 실패 → IAP 검증 전면 장애 위험 | `api` 서비스 volumes에 `./secrets:/app/secrets:ro` 추가 |
| M-23 | `Framework.Infrastructure/Persistence/AppDbContext.cs:165` | S15 | RewardGrant→Player FK OnDelete 명시 누락 — EF Core 기본 Cascade, 미래 hard delete 경로 추가 시 데이터 유실 위험 | 명시적 `OnDelete(DeleteBehavior.Restrict)` 설정 |
| M-24 | CI 파이프라인 부재 | S16 | gitleaks 등 시크릿 자동 스캐너 미통합 | pre-commit hook 또는 GitHub Actions에 gitleaks 통합 |
| M-25 | CI 파이프라인 부재 | S17 | `dotnet list package --vulnerable` CI 자동 실행 없음 | CI에 `--vulnerable --include-transitive` 단계 추가 |
| M-26 | `Framework.Api/Controllers/Player/IapRtdnController.cs:66-109` | A2 | Base64 디코딩·JSON 역직렬화·PackageName 검증 인라인 — Controller가 인프라 로직 직접 처리, 테스트 불가 | `IIapRtdnPayloadParser` 등 파서 분리, Controller는 ParseResult만 수신 |
| M-27 | `Framework.Api/Controllers/Player/IapRtdnController.cs`, `AdsCallbackController.cs` | Q3 | IapRtdn 8건·AdsCallback 5건 익명 객체(`new { ok, reason }`) 응답 잔존 — Pub/Sub·광고 네트워크 콜백이라 외부 규격 제약 있으나 DTO 타입 부재 | 전용 응답 record 타입 정의(OkResponse/FailResponse), 익명 객체 제거 |
| M-28 | `Framework.Application/Features/Item/ItemMasterService.cs` | AX-Cache | `IItemMasterCache` 미구현 — 핫패스 아이템 조회가 매 요청 DB 직접 조회. LevelTableProvider와 달리 캐시 레이어 없음 | `IMemoryCache` 기반 아이템 마스터 캐시 도입, Admin 아이템 수정 시 Invalidate |
| M-29 | `Framework.Domain/Entities/Mail.cs:35` | A11 | `Mail.ItemId`/`Item` deprecated 네비게이션 잔존 — 디케이 일정 없이 코드에만 주석. Currency-as-Item 완료 후 단일 경로(`MailItems`)만 존재해야 하나 구 경로 혼재 | 디케이 일정 확정 후 `Mail.ItemId`/`Item` 컬럼 마이그레이션으로 제거 |
| M-30 | `Framework.Api/Program.cs:175` | A10 | 점검 모드 처리가 `app.Use(async (context, next) => {...})` 인라인 람다 — 응집도·예외 안전성 부족, 단위 테스트 불가 | `MaintenanceMiddleware` 클래스 분리 |
| M-31 | `Framework.Application/Features/Auth/AuthService.cs` | AX-Observability | `AuthService`에 `ILogger` 미주입 — 로그인/탈퇴/토큰 발급 등 보안 핵심 경로 감사 로그 전무 | `ILogger<AuthService>` 주입, 로그인 성공·실패·탈퇴·연동 등 주요 이벤트 기록 |
| M-32 | `Framework.Infrastructure/Repositories/NoticeRepository.cs:23`, `InquiryRepository.cs:27` | Q6 | `GetAllAsync()` 페이지네이션 없음 — 공지사항·문의가 대량 누적 시 전체 로딩. `MailRepository.GetByPlayerIdAsync`도 동일 | 페이지네이션 파라미터 추가 또는 최대 로딩 한도 적용 |
| M-33 | `Framework.Application/Features/MatchMaking/MatchDto.cs:6` | S4 | `JoinMatchRequestDto.Tier`/`HumanType` enum 검증 부재 — 정의되지 않은 정수값 바인딩 시 기본값 silently 사용 | `[JsonConverter(typeof(JsonStringEnumConverter))]` 또는 `[EnumDataType]` 적용 |
| M-34 | `Framework.Application/Features/Iap/IapRtdnService.cs:97,152,161` | S11 | `PurchaseToken` 평문 로깅 — 결제 토큰이 로그 시스템에 원문 노출. `MaskToken` 헬퍼 존재하나 미사용 | `notification.PurchaseToken` → `notification.PurchaseToken.MaskToken()` (또는 앞 8자+`***`) |
| M-35 | `Framework.Api/Controllers/Player/IapRtdnController.cs:41` | S13 | RTDN `MessageId` 캐시 dedup 부재 — Pub/Sub 재전송 시 동일 알림 중복 처리를 비즈니스 멱등성(IapPurchase UNIQUE)에만 의존. 네트워크 순간 중복 시나리오에서 불필요한 DB 부하 | `IMemoryCache`로 최근 N분 MessageId 캐시, 중복 즉시 200 반환 |

---

## 7장. Low / 추적 항목

| ID | 위치 | 점검 ID | 설명 |
|---|---|---|---|
| L-1 | `Framework.Domain/Common/Results/Result.cs, Error.cs` | A2 | Result&lt;T&gt;/Error 채택률 0%, dead code 위험 — 신규 기능 적용 가이드 필요 |
| L-2 | `Framework.Domain/Common/Guards/Guard.cs` | A2 | Guard 채택률 0% — 도메인 엔티티 생성자 적용 권장 |
| L-3 | `Framework.Api/Controllers/Player/AuthController.cs:118-121` | A2 | InvalidOperationException catch 안티패턴(M-8과 연동) |
| L-4 | `Framework.Application/Common/SourceKeys.cs:25-32` | A7 | Stage SourceKey 메서드 3종이 비-Content 위치 |
| L-5 | `Framework.Application/BackgroundServices/PiiRetentionHealthState.cs:9-15` | A5 | Singleton 공유 상태 volatile/lock 부재 |
| L-6 | `Framework.Application/Features/Exp/LevelTableProvider.cs:65` | AX-Cache | 캐시 미스 시 sync-over-async 패턴 |
| L-7 | `Framework.Application/BackgroundServices/PiiRetentionCleanupService.cs:20` | 동시성 | advisory lock 미적용 — 단일 인스턴스 환경에서는 안전, 스케일아웃 시 재설계 필요 |
| L-8 | `Framework.Api/Middleware/AuthDomainExceptionHandler.cs:56` | A9 | JsonSerializer 옵션 미지정 |
| L-9 | `Framework.Api/Program.cs:189` | A10 | 점검 503 응답이 ProblemDetails가 아닌 단순 JSON |
| L-10 | `Framework.Api/Services/IapStore/IapConsumerResolver.cs:19` | A8 | 다른 Resolver와 달리 FirstOrDefault O(N) 패턴 불일치 |
| L-11 | `Framework.Domain/Constants/CurrencyIds.cs` | A11 | CurrencyIds 상수 정의 후 사용처 0건 |
| L-12 | `Framework.Domain/ValueObjects/RewardBundle.cs:16` | A11 | IsCurrencyOnly 명명이 Currency-as-Item 도입 후 의미 모순 |
| L-13 | `Framework.Api/Controllers/Admin/AdminNoticesController.cs:44` | Q2 | Delete/Update 성공 시 Ok() — 다른 컨트롤러와 NoContent() 불일치 |
| L-14 | Admin DTO 다수 | Q3 | NoticeDto/ShoutDto 등 Admin DTO [Required]/[MaxLength] 미부착 |
| L-15 | `Framework.Api/Controllers/Content/Player/StagesController.cs:73` | Q4 | KeyNotFoundException/InvalidOperationException 범용 catch |
| L-16 | Controller 대부분 | AX-Observability | 개별 비즈니스 이벤트 로깅 누락(SerilogRequestLogging이 요청 레벨은 커버) |
| L-17 | `Framework.Api/Middleware/GlobalExceptionHandler.cs:35` | AX | Development 환경에서 LogError 미기록 |
| L-18 | `Framework.Application/Features/Reward/RewardCancelService.cs:106` | Q4 | Exception 범용 catch — 의도적 설계이나 패턴 주의 |
| L-19 | Admin UI 5건 | Q7 | DateTime.Now 사용(표시용, 서버 로직 무관) |
| L-20 | Admin UI 2건 | Q7 | DateTime.Today 사용(필터 기본값, 서버 로직 무관) |
| L-21 | `Framework.Admin/` 빌드 | 빌드 | CS8604 nullable 경고, CS0162 접근 불가 코드 경고 |
| L-22 | `Framework.Admin/Components/Pages/Admin/MatchMaking.razor.cs:18` | Q9 | SafeComponentBase 미상속(Release SignalR 비활성이라 실위험 제한적) |
| L-23 | `Framework.Admin/Components/Account/Login.razor.cs:9` | Q9 | SafeComponentBase 미상속 — 로그인 실패 예외 시 회로 끊김 가능 |
| L-24 | `Framework.Admin/Components/Pages/InquiryTest.razor.cs:15` | Q9 | SafeComponentBase 미상속(테스트 페이지, 최저 우선순위) |
| L-25 | `Framework.Tests/Unit/Smoke/DiSmokeTests.cs:34` | AX-Test | ValidateOnBuild 미적용 — DEVNOTES 기술 부채 확인 |
| L-26 | `Framework.Api/Extensions/ServiceExtensions.cs:332` | S1 | ClockSkew 미설정 — 기본 5분 허용 |
| L-27 | `Framework.Api/Services/JwtTokenProvider.cs:25` | S1 | HS256 대칭키 — 운영 키 64+바이트 및 정기 로테이션 절차 문서화 필요 |
| L-28 | `Framework.Api/Controllers/Player/AuthController.cs:44` | S2 | PlayerBannedException 401/403 불일치(보안이 아닌 UX 디자인 이슈) |
| L-29 | `Framework.Api/Hubs/MatchMakingHub.cs:9` | S7 | SignalR 허브 메시지 단위 Rate Limit 부재 — DEVNOTES 추적 항목 |
| L-30 | `Framework.Api/Services/IapStore/GooglePlayStoreVerifier.cs:124` | S11 | 외부 라이브러리 예외 메시지가 로거에 흘러들 수 있음 |
| L-31 | `Framework.Api/appsettings.Development.json:18` | S8 | JWT Dev 시크릿 평문 커밋 — Production 사용 가이드 강화 필요 |
| L-32 | 모든 .csproj | S17 | transitive 의존 포함 동적 NuGet Audit 미수행 — 배포 전 수동 실행 권고 |

---

## 8장. DEVNOTES.md 갱신 권고

| 항목 | 유형 | 내용 |
|---|---|---|
| PiiRetentionHealthCheck 통합 | **삭제** | "[미구현] HealthCheck 통합 미적용" 항목 — P2.2에서 `PiiRetentionHealthCheck.cs` 구현 완료 확인. 해당 항목 제거 |
| round_20260507 이슈 처리 현황 | **추가** | "§ REVIEW_REPORT.md 우선순위 처리 결과 (round_20260507)" 섹션 추가 — H-1(광고 SecretKey)/H-2(IAP 디스크 I/O)/M-22(secrets 볼륨) 즉시 처리 대상으로 기록 |
| docker-compose Admin 서비스 정의 부재 | **추가** | M-21 — Admin 컨테이너 IaC 격차를 "[기술 부채]"에 추가. `docker-compose.yml`에 admin 서비스 정의 및 환경변수 주입 필요 |
| GooglePlayClientFactory Singleton 캐싱 | **추가** | H-2 — "[기술 부채]"에 추가: 매 호출 디스크 I/O → Singleton/Lazy 캐싱 전환 필요 |

---

## 부록 A. 청크 산출물 인덱스

| 청크 | 파일 | 주요 판정 | 이슈 |
|---|---|---|---|
| P1.1 의존성·책임·인터페이스 | `review/round_20260507/p1_1_dependencies.md` | A1 PASS / A2 WARN / A3 PASS | Med 3, Low 3 |
| P1.2 DTO·DI·트랜잭션·Content·캐시 | `review/round_20260507/p1_2_dto_di_tx_content.md` | A4/A5/A6/AX-Cache PASS / A7 WARN | Med 2, Low 4 |
| P1.3 Strategy·인증·횡단·Currency-as-Item | `review/round_20260507/p1_3_strategy_auth_xcut.md` | A8/A10/A11 PASS / A9 WARN | Med 1, Low 6 |
| P2.1 Controllers + 관측성 | `review/round_20260507/p2_1_controllers.md` | Q1/Q5/Q10 PASS / Q2/Q3/Q4/AX WARN | Med 4, Low 5 |
| P2.2 Services + 시간/타임존 | `review/round_20260507/p2_2_services.md` | Q1/Q4/Q5/Q7/AX-Time PASS / Q10 WARN | Med 2, Low 3 |
| P2.3 Repositories + Migrations + 동시성 | `review/round_20260507/p2_3_repos_migrations_dbcontext.md` | Q6/Q8/AX-Migration/AX-Concurrency PASS | Med 2, Low 2 |
| P2.4 Razor + 테스트 커버리지 | `review/round_20260507/p2_4_razor_tests.md` | Q9/AX-Test WARN | Med 4, Low 4 |
| P3.1 인증·인가·디버그 | `review/round_20260507/p3_1_authn_authz_debug.md` | S2/S3 PASS / S1 WARN | Med 2, Low 3 |
| P3.2 입력·SQLi·IDOR·Rate | `review/round_20260507/p3_2_input_sqli_idor_rate.md` | S5/S6/S7 PASS / S4 WARN | Med 2, Low 2 |
| P3.3 시크릿·외부검증·OIDC·로깅·회복탄력성 | `review/round_20260507/p3_3_secrets_external_oidc_log.md` | S9/S10/S11 PASS / S8/AX-Resilience WARN | **High 2**, Med 4, Low 2 |
| P3.4 CORS·멱등·Admin·개인정보 | `review/round_20260507/p3_4_cors_idem_admin_pii.md` | S12/S13/S14/S15 PASS | Med 4, Low 1 |
| P3.5 도구 | `review/round_20260507/p3_5_tools.md` | S17 PASS / S16 WARN | Med 3, Low 1 |
