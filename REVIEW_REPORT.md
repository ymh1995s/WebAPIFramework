# WebAPIFramework 종합 리뷰 보고서

> 검토 일시: 2026-04-30 | 이슈만 기재 (합격 항목 제외)

---

## 처리 순서

| 순서 | ID | 항목 | 이유 |
|---|---|---|---|
| ~~1~~ | ~~C-1~~ | ~~UnitOfWork 중첩 트랜잭션 수정~~ | ✅ 완료 |
| ~~2~~ | ~~C-2~~ | ~~DispatchMailAsync Currency 지급 구현~~ | ✅ 완료 (기완료 확인 — Mail 엔티티 Gold/Gems/Exp 컬럼 + ClaimAsync 처리) |
| ~~3~~ | ~~H-1~~ | ~~MatchMakingHub `[Authorize]` 추가~~ | ✅ 완료 (기완료 확인) |
| ~~4~~ | ~~H-2~~ | ~~AdminApiKeyAttribute FixedTimeEquals~~ | ✅ 완료 (기완료 확인) |
| ~~5~~ | ~~H-9~~ | ~~AuthService 다단계 쓰기 트랜잭션화~~ | ✅ 완료 (기완료 확인) |
| ~~6~~ | ~~H-3~~ | ~~Repository SaveChanges 패턴 통일~~ | ✅ 완료 |
| ~~7~~ | ~~H-5~~ | ~~AuthController Repository 주입 제거~~ | ✅ 완료 |
| ~~8~~ | ~~M-14~~ | ~~`int.Parse` → `int.TryParse` 12개소~~ | ✅ 완료 |
| ~~9~~ | ~~M-10~~ | ~~SubmitInquiryDto `[MaxLength]` 추가~~ | ✅ 완료 |
| ~~10~~ | ~~M-11~~ | ~~인게임 API Rate Limiting 추가~~ | ✅ 완료 |
| ~~11~~ | ~~H-6~~ | ~~RTDN clawback Admin 알림~~ | ✅ 완료 |
| ~~12~~ | ~~M-12~~ | ~~Admin 비밀번호 해싱~~ | ✅ 완료 |
| ~~13~~ | ~~M-9~~ | ~~N+1 쿼리 제거~~ | ✅ 완료 |
| ~~14~~ | ~~M-8~~ | ~~AdminPlayerService DB 페이지네이션~~ | ✅ 완료 |
| ~~15~~ | ~~H-4~~ | ~~IExpService/ILevelTableProvider Domain → Application 이동~~ | ✅ 완료 |
| — | 우선순위 목록 전체 완료 — Medium/Low 백로그만 잔여 | | |

---

## Critical

### C-1. UnitOfWork 중첩 트랜잭션 — IAP 결제 경로 실패

- **파일**: `Framework.Infrastructure/Persistence/UnitOfWork.cs:22-25`
- **경로**: `IapPurchaseService.VerifyAndGrantAsync:80` → `RewardDispatcher.GrantAsync:63` → 두 번째 `BeginTransactionAsync()`
- **현상**: `UnitOfWork`가 기존 트랜잭션 여부 검사 없이 덮어씀 → Npgsql `InvalidOperationException`
- **해결**: `UnitOfWork.BeginTransactionAsync`에 `_transaction != null` 재진입 방지 추가. `RewardDispatcher`는 트랜잭션을 직접 시작하지 않고 호출자에게 위임

### C-2. DispatchMailAsync Currency 지급 누락 — 재화 소실

- **파일**: `Framework.Application/Features/Reward/RewardDispatcher.cs:238-241`
- **현상**: `DispatchMode.Mail`에서 Gold/Gems/Exp를 우편 텍스트에만 기재. `MailService.ClaimAsync`에 처리 없음 → 수령해도 재화 미지급
- **영향**: Admin 수동 보상 지급에서 Mail 모드 + Currency 선택 시 재화 소실
- **해결**: `MailItem` 기반으로 Currency 아이템화하거나 `ClaimAsync`에서 Currency 타입 처리 추가

---

## High

### H-1. MatchMakingHub `[Authorize]` 미적용

- **파일**: `Framework.Api/Hubs/MatchMakingHub.cs:7`, `Program.cs:178`
- **현상**: Hub 클래스 및 `MapHub` 모두 인가 없음. 비인증 사용자 SignalR 연결 가능
- **해결**: Hub에 `[Authorize]` 추가 또는 `MapHub(...).RequireAuthorization()`

### H-2. AdminApiKeyAttribute 타이밍 공격

- **파일**: `Framework.Api/Filters/AdminApiKeyAttribute.cs:25`, `Program.cs:136`
- **현상**: `key != expectedKey` 일반 문자열 비교. Admin Key가 단일 방어선인데 타이밍 공격 노출
- **해결**: `CryptographicOperations.FixedTimeEquals` 적용 (두 곳 모두)

### H-3. Repository SaveChangesAsync 자동 호출 패턴 혼재

- **파일**: `PlayerRepository.cs:38,45,68,80,95,106` / `PlayerProfileRepository.cs:25,32` / `RefreshTokenRepository.cs:25,32,40`
- **현상**: 해당 Repository의 Add/Update/Delete가 내부에서 즉시 `SaveChangesAsync()` 호출. 다른 Repository는 명시 호출 방식 → 트랜잭션 내 의도치 않은 flush
- **해결**: 자동 호출 제거. 호출자(Service)에서 명시적으로 `SaveChangesAsync` 또는 `UoW.CommitAsync` 호출

### H-4. Application 서비스 인터페이스가 Domain에 위치

- **파일**: `Framework.Domain/Interfaces/IExpService.cs`, `Framework.Domain/Interfaces/ILevelTableProvider.cs`
- **현상**: Application 관심사 인터페이스가 Domain 레이어에 위치. Clean Architecture 위반
- **비고**: Content 영역 역참조 회피 목적일 수 있음 — 이동 전 의존성 확인 필요
- **해결**: `Framework.Application/Features/Exp/` 로 이동

### H-5. AuthController에 Repository 직접 주입 + 예외에 도메인 엔티티 노출

- **파일**: `Framework.Api/Controllers/Player/AuthController.cs:19-25,152-177`, `Framework.Application/Features/Auth/GoogleAccountConflictException.cs:10-13`
- **현상**: Controller가 `IPlayerProfileRepository`를 직접 주입. `GoogleAccountConflictException`이 `Player` 엔티티를 보유하고 Controller에서 필드 직접 접근
- **해결**: `BuildConflictDtoAsync` 로직을 `AuthService`로 이동. 예외는 경량 record 보유

### H-6. RTDN 환불 clawback 미구현

- **파일**: `Framework.Application/Features/Iap/IapRtdnService.cs:91-96`
- **현상**: 환불 감지 시 `Status=Refunded` 기록만. 기 지급 보상 회수 없음
- **해결 (1단계)**: 환불 감지 시 Admin Slack/이메일 알림 발송. 자동 회수는 중기 로드맵

### H-9. AuthService 비트랜잭션 다단계 쓰기

- **파일**: `Framework.Application/Features/Auth/AuthService.cs:43-52`
- **현상**: `GuestLoginAsync`에서 Player 저장(SaveChanges) → Profile 저장(SaveChanges) 독립 실행. 중간 실패 시 Player만 생성된 불완전 상태 발생
- **해결**: UoW로 감싸 단일 트랜잭션으로 처리

---

## Medium (백로그)

| ID | 항목 | 파일 |
|---|---|---|
| M-1 | Application의 EF Core 직접 의존 — `DbUpdateException` catch 7개 파일 | AdPolicyService, IapPurchaseService 등 |
| M-2 | `IsUniqueViolation` 5개 파일 중복 (미세 차이 포함) | RewardDispatcher, IapPurchaseService, IapProductService, AdPolicyService, RewardTableService |
| M-3 | X-Admin-Key 검증 로직 2곳 중복 | AdminApiKeyAttribute.cs, Program.cs:136 |
| M-4 | RateLimitLogRepository 인터페이스 없이 구체 클래스 등록 | ServiceExtensions.cs:309 |
| M-5 | CancellationToken 전파 전무 (Application 전체) | Application Features 전체 |
| M-6 | AsNoTracking 전역 미사용 (읽기 쿼리 전반) | Infrastructure Repositories 전체 |
| M-7 | N+1 쿼리 | RewardTableService.cs:57-59, AuditLogService.cs:65-68 |
| M-8 | AdminPlayerService 전체 로드 후 메모리 페이지네이션 | AdminPlayerService.cs:36-37 |
| M-9 | AuthService.ResolveGoogleConflictAsync 비트랜잭션 | AuthService.cs:200-206 |
| M-10 | SubmitInquiryDto `[Required]`, `[MaxLength]` 없음 | InquiryDto.cs:4 |
| M-11 | 인게임 API Rate Limiting 미적용 | MailsController, InquiriesController, StagesController 등 |
| M-12 | Admin 비밀번호 평문 저장/비교 | Admin/Program.cs:116 |
| M-13 | Admin 로그인 CSRF 보호 비활성화 | Admin/Program.cs:124 |
| M-14 | `int.Parse(User.FindFirst("playerId")!.Value)` null 강제 언래핑 12개소 | AuthController, MailsController 등 |
| M-15 | MailDto에 MailItems 미포함 — 다중 아이템 우편 클라이언트 미노출 | MailDto.cs |
| M-16 | LevelTableProvider `GetAwaiter().GetResult()` 동기 블로킹 | LevelTableProvider.cs:65 |

---

## Low (추적)

| ID | 항목 | 파일 |
|---|---|---|
| L-1 | SourceKey 패턴 Magic String 분산 | IapPurchaseService, ExpService, StageClearService 등 |
| L-2 | PlayerRepository.BanAsync 조용한 실패 (`if null return`) | PlayerRepository.cs:60-81 |
| L-3 | LevelThresholdRepository 자체 트랜잭션 (UoW 이중화) | LevelThresholdRepository.cs:27-53 |
| L-4 | Content/Framework 분리 — 폴더 컨벤션만, 컴파일러 강제 없음 | 전체 구조 |
| L-5 | Admin 단일 키 체계 — 중장기 RBAC 전환 검토 | AdminApiKeyAttribute |
| L-6 | RefreshToken 재사용 탐지 (token family) 미구현 | JwtTokenProvider, AuthService |
| L-7 | CORS 정책 미설정 — 웹 클라이언트 추가 시 명시 필요 | Program.cs |
| L-8 | 보안 헤더 미설정 (X-Content-Type-Options 등) — Caddy에서 추가 가능 | Program.cs |
| L-9 | `dotnet list package --vulnerable` CI/CD 자동화 | — |
| L-10 | `IapPurchaseService.cs:168` `"google:"` 리터럴 하드코딩 | IapPurchaseService.cs:168 |
