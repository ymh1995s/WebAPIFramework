# Dev Notes

> **문서 구성** (2026-05-05 분할):
> - **본 문서 (DEVNOTES.md)** — 설계 결정 박제 / 기술 부채 / 미구현 / Feature Status (내부 박제 중심)
> - **`SERVER_GUIDE.md`** — 서버 운영자 절차 (시크릿 / 배포 / 마이그레이션 / SystemConfig / 이슈 대응)
> - **`CLIENT_GUIDE.md`** — Unity 클라이언트 구현 walkthrough (앱 부팅~결제 순차)

## Feature Status

| 기능 | 설명 |
|---|---|
| JWT 인증 | 게스트 로그인(DeviceId), AccessToken/RefreshToken 발급, 로그아웃 |
| 구글 OAuth 연동 | Google IdToken 검증, 신규 로그인 및 기존 계정 연결, 계정 충돌 감지(409)/해소, 게스트 계정 소프트 딜리트 |
| 게스트 결제 차단 | `[RequireLinkedAccount]` 필터 — 구글 미연동 계정의 결제 엔드포인트 접근 시 403 반환 (결제 컨트롤러 구현 시 부착 필요) |
| 랭킹 시스템 | 게임 결과 점수 기록, 상위 N명 랭킹 조회 |
| 인벤토리 관리 | 플레이어 보유 아이템 조회, 아이템 획득. Gold/Gems는 PlayerItem(ItemId=1/2)으로 관리되므로 인벤토리 조회 시 ItemType.Currency 항목이 재화로 함께 반환됨 |
| 우편 시스템 | 우편 수신/수령 API, Admin 단건·일괄 발송 |
| 일일 로그인 보상 | 로그인 시 당일 보상 우편 발송 (이번 달 로그인 횟수 기반, 매월 리셋). 빈 일자는 보상 없음. Current/Next 2슬롯 방식으로 이번 달·다음 달 보상 예약 관리. KST 하루 기준 시각(기본 00:00) Admin 설정 가능 |
| 매치메이킹 | SignalR 기반 실시간 매칭, 대기열 관리 |
| 보상 프레임워크 | 범용 보상 파이프라인 — 모든 보상 경로를 단일 IRewardDispatcher로 통합. 선기록 멱등성, Direct/Mail/Auto 분기, RewardTable 마스터 관리, Admin 수동 지급/우편 발송 통합 페이지 |
| 아이템 마스터 관리 | Admin CRUD (추가/수정/소프트삭제), 보유 유저 수 확인 |
| Admin 인증 | X-Admin-Key 헤더 기반 API 접근 제어. Admin 컨트롤러는 `[AdminApiKey]` 필터로 보호 (JWT [Authorize]와 독립). 점검 미들웨어에서 X-Admin-Key 확인 시 503 면제. Admin 로그인 비밀번호는 BCrypt 해시 검증(`AdminPasswordVerifier`, `Admin:PasswordHash` 키) |
| 시스템 설정 | 점검 모드, 앱 버전, 일일 보상 기준 시각 등 SystemConfig Admin 제어 |
| 어뷰징 방어 | auth 엔드포인트 Rate Limiting (IP 기준, `RateLimiting:AuthPermitLimit` 설정), 429 발생 시 PlayerId·UserAgent 포함 DB 로그. Admin 보안 감시 — 통합 타임라인(Rate Limit 초과 / 재화 이상치 / 계정 정지 이벤트 병합), IP 집계, 타임라인에서 직접 영구밴 가능. 인게임 API: `game` 정책 PlayerId 기준 120회/분, IAP 검증: `iap-verify` 정책 20회/분 |
| 점검 모드 | 수동 ON/OFF 및 시각 예약, 미들웨어에서 503 차단, Admin은 점검 중에도 접근 가능 |
| 계정 탈퇴 | DELETE /api/auth/withdraw, SoftDelete + PII 익명화 처리. Player 행 보존(IapPurchase FK 유지), 게임 진행 데이터 hard delete, 멱등 재호출 204 반환 (H-12 round_20260503) |
| 클라이언트 앱 버전 체크 | GET /api/version/check, 강제 업데이트 여부 반환, Admin에서 최소/최신 버전 설정 (서버 버전 아님 — 앱스토어 배포 Unity 빌드 기준) |
| 공지/1회 공지 시스템 | **공지**: `GET /api/notices/latest` 최신 활성 공지 1개 반환. 클라이언트가 NoticeId를 PlayerPrefs에 저장해 1회성 팝업 표시. Admin CRUD. **1회 공지**: Admin에서 전체/특정 플레이어 대상 HUD 텍스트 발송. DB 이력 기록. 클라이언트 접속 시 `GET /api/shouts/active` 1회 호출(폴링 방식) — 만료 시간 내 활성 1회 공지 수신. Admin `/notices`, `/shouts` 페이지 별도 관리 |
| 플레이어 문의 | POST /api/inquiries 제출, GET /api/inquiries 내 목록 조회. Admin 답변 등록. 소원수리함 형태(자유 텍스트). Blazor 테스트 페이지 포함 |
| 감사 로그 | 재화/아이템 변동 추적. Item.AuditLevel(AnomalyOnly/Full) + AnomalyThreshold 기준으로 저장 범위 차별화. Admin `/audit-logs` 페이지에서 플레이어·아이템·기간·이상치 필터 조회. 현재 훅은 `MailService.ClaimAsync` 적용. Gold/Gems는 Currency-as-Item 전환(ItemId=1/2)으로 감사 로그 기록 가능. Exp는 단조 증가 자원이므로 AuditLog 대상 아님 — 어뷰징 추적은 `RewardGrants(SourceKey="levelup:{N}")`로 위임 |
| 광고 SSV 보상 | Unity Ads / IronSource SSV(Server Side Verification) 콜백 검증 및 보상 지급. Strategy 패턴으로 모듈화 — 새 네트워크 추가 시 검증기 클래스 1개 + DI 등록 1줄. HMAC-SHA256 서명 검증, 일일 한도 제한, RewardDispatcher 멱등성 보장. Admin `/ad-policies` 페이지에서 PlacementId별 보상 정책 CRUD 관리. 콜백 URL: `GET /api/ads/callback/unity-ads`, `GET /api/ads/callback/ironsource` |
| 트랜잭션 추상화 | `IUnitOfWork` 인터페이스(Domain) + `UnitOfWork` 구현체(Infrastructure). RewardDispatcher가 IUnitOfWork를 통해 전체 보상 지급을 단일 트랜잭션으로 보장 |
| 인앱 결제(IAP) | Google Play 영수증 서버 검증 및 보상 지급. Strategy 패턴으로 스토어별 모듈화(현재 Google Play 구현, Apple 예약). OIDC 기반 RTDN(환불 알림) 수신 및 자동 환불 처리. Admin `/iap-products` 상품 관리, `/iap-purchases` 구매 이력 조회. API: `POST /api/iap/google/verify`, `POST /api/iap/google/rtdn`. Rate Limit: iap-rtdn 600회/분 |
| 레벨/경험치 | `IExpService` — Exp 누적, 임계값 초과 시 자동 레벨업 + 레벨업 보상 지급(`SourceKey="levelup:{level}"`). 다중 레벨업 while 루프. 임계값은 `LevelThresholds` DB 테이블로 외부화 — Admin `/level-thresholds` 페이지에서 CRUD 관리, `ILevelTableProvider`(Singleton 캐시) 통해 런타임 조회 |
| 스테이지 클리어 [컨텐츠] | `POST /api/stages/{stageId}/complete`. 순차 진행 조건(`RequiredPrevStageId`), 최초 클리어 보상 + 재클리어 보상 감소(decay%), Exp/레벨업 연동. Admin 스테이지 마스터 CRUD. **`Content/` 영역 분리** — 게임 컨텐츠 코드, Framework 영역에서 참조 금지 |
| 운영 알림(AdminNotification) | RTDN 환불 등 운영 이슈를 Admin에 즉시 통지. `AdminNotification` 엔티티 + Repository/Service 전 레이어 구현. API: `GET /api/admin/notifications/unread-count`, `GET /api/admin/notifications`, `POST /:id/read`, `POST /:id/unread`, `POST /read-all`. Admin UI: 헤더 `NotificationBell`(30초 폴링), `/admin-notifications` 페이지(필터/페이지네이션/읽음토글). RTDN 환불 시 자동 생성 — `Voided=Critical`, `Canceled=Warning` |

---

## 운영 절차 — `SERVER_GUIDE.md` 참조

다음 항목은 `SERVER_GUIDE.md`로 이전됨 (운영자 절차 중심):
- Framework.Api / Framework.Admin 시크릿 교체값 (`.env` 키 매핑)
- Google Play 연동 준비 (서비스 계정 / Pub/Sub / License Testers)
- DB 인덱스 미적용 항목 (성능 튜닝 시점)
- SystemConfig 전체 키 (점검 모드 / 앱 버전 / 일일 보상)
- 마이그레이션 표준 절차 (xmin 수동 절차 포함)
- Test 프로젝트 가이드
- 배포 체크리스트
- 운영 중 이슈 대응 매트릭스


## [설계 결정]

### 계정 탈퇴 정책 — SoftDelete + PII 익명화 (round_20260503)

- Player 행은 hard delete하지 않고 `IsDeleted=true` + `DeletedAt` 기록으로 SoftDelete
- 익명화 범위: `DeviceId`/`GoogleId` NULL, `Nickname` `"탈퇴유저-{Id}"`. PublicId/Id/IsBanned/통계 필드는 보존(밴 회피 차단 + 결제 추적)
- IapPurchase는 Restrict FK 유지 — 전자상거래법 5년 보관 의무 충족
- 게임 진행 데이터(Profile/Item/Mail/MailItem/DailyLoginLog/GameResult참가/RewardGrant/Inquiry/Shout/StageClear)는 즉시 hard delete — 재가입 시 데이터 복구 차단
- AuditLog/BanLog/IapPurchase는 손대지 않음 (운영 추적 + 결제 보존)
- 멱등성: 이미 탈퇴된 계정의 `WithdrawAsync` 재호출은 204 즉시 반환 (네트워크 재시도 안전)
- 배경: H-12(IapPurchase Restrict ↔ hard delete FK 충돌). round_20260503 보고서 5장 참조


### PII 자동 보관기간 정책 (round_20260503, M-44)

- **법적 근거**:
  - 개인정보보호법 §21 — 목적 달성 후 지체없이 파기
  - 안전성 확보조치 기준 §8 (2025-10-31 시행, 고시 2025-9호) — 접속기록 1년 이상 (5만명↑ 2년)
  - 통신비밀보호법 시행령 §41 — 인터넷 로그 3개월 이상
  - 전자상거래법 시행령 §6 — 거래기록 5년

- **테이블별 보관기간**:
  - `AuditLog`: 365일 hard delete
  - `RateLimitLog`: 90일 hard delete
  - `BanLog.ActorIp`: 365일 후 NULL 익명화 (행 영구 보존 — 재밴 추적)
  - `IapPurchase.ClientIp`: 상태 종결(Granted/Refunded/Failed) + 90일 후 NULL 익명화 (본문 5년 보존)

- **구현**: `Framework.Application/BackgroundServices/PiiRetentionCleanupService.cs`
  - 매일 KST 03:00 실행, 5000행 청크 ExecuteDelete/Update
  - 비상 정지: `appsettings.json` 의 `PiiRetention.Enabled = false`

- **운영 정책 변경 시**: `appsettings.json` 의 `PiiRetention` 섹션 값 조정 (재배포 필요)

- **단일 인스턴스 가정**: 다중 컨테이너 운영 시 PostgreSQL advisory lock 도입 필요 (별도 라운드)

- **HealthCheck 통합 미적용**: BackgroundService 정지 시 보관기간 위반 위험. 별도 라운드에서 IHealthCheck 등록 + Admin 알림 권고


### 보상 프레임워크

#### 확정 사항

| # | 결정 | 근거 |
|---|---|---|
| 1 | Gold/Gems — PlayerItem(ItemId=1/2)으로 이동(Currency-as-Item). 통화 여부는 `ItemType.Currency`로 판별. 새 통화 추가 = Item 마스터 행 추가만으로 완료, 스키마 변경 불필요 | 재화 종류 확장 시 스키마 변경 필요 제거 |
| 2 | Exp/Level — PlayerProfile 컬럼 유지. Exp는 차감 불가 단조 증가 자원이므로 Currency-as-Item 패턴 부적합. Level은 Exp의 파생값(캐시 컬럼)이므로 분리 불가 | Exp Item화 시 Exp/Level이 두 테이블에 쪼개져 동기화 책임 발생. 도메인적으로 Exp는 자원이 아닌 진행 상태 |
| 2 | MailItems 테이블 도입 — 1통에 N종 아이템 묶음 발송 | 다중 보상 UX·트랜잭션 일관성 |
| 3 | PlayerRecord 폐기 → GameResultParticipants로 대체 | 랭킹 데이터 미존재, 매치 식별자 없는 기존 구조 한계 |
| 4 | DailyLogin 보상은 `IRewardDispatcher.GrantAsync(Mode=Mail)` 단일 경로 (M-17 round_20260503 전환). DailyLoginLog는 로그인 통계용으로만 유지 | RewardDispatcher 멱등성(SourceKey=`daily-login:{yyyy-MM-dd}`)으로 일관. 1차 DailyLoginLog 커밋 → 2차 Dispatcher 분리 트랜잭션, 2차 실패 시 AdminNotification(Critical, RewardDispatchFailure)으로 운영자 수동 보전 |

#### 공통 파이프라인

```
[호출자: 빈 번들·PlayerId 검증]
  → [UnitOfWork 트랜잭션 진입]
  → [RewardGrant 선기록 + UNIQUE 위반 catch → Duplicate 반환]
  → [DetermineMode: Auto면 IsCurrencyOnly로 Direct/Mail 자동 결정]
  → [Direct: PlayerItem(Gold=ItemId1, Gems=ItemId2) 수량 증가 + IExpService.AddExpAsync 위임 → 레벨업 발생 시 GrantLevelUpRewardsAsync로 레벨업 보상 지급]
    또는
   [Mail: Mail + MailItems 생성 → 수령 시 ClaimAsync에서 실지급]
  → [Mail 모드면 grant.MailId 업데이트]
  → [트랜잭션 커밋 + 결과 반환]
```
※ Source별 권한/사전 검증은 호출자(Service) 책임. RewardDispatcher는 멱등성·지급·트랜잭션만 담당
※ AuditLog 기록: `MailService.ClaimAsync`(MailClaim 사유) + `RewardDispatcher.DispatchDirectAsync`(Direct 보상 전체) 두 경로에서 수행

#### 레벨업 처리

- Exp는 PlayerProfile 컬럼 유지 (Item화하지 않음)
- `IExpService` (위치: `Framework.Application/Features/Exp/`)가 Exp 누적 → 임계값 초과 시 Level 증가 → **오른 레벨 번호 목록 반환** (보상 지급은 호출자 책임)
- 레벨 임계값은 `LevelThresholds` 테이블 + `ILevelTableProvider` Singleton 캐시
- 레벨업 보상은 `RewardDispatcher.GrantLevelUpRewardsAsync`가 담당 — `RewardTable`에서 `Code="levelup:{level}"`로 조회, `SourceKey="levelup:{level}"`로 멱등 보장
- 다중 레벨업 while 루프 지원 (한 번의 AddExp 호출로 여러 레벨 가능)
- **의존 방향**: `RewardDispatcher → IExpService` 단방향 (DI 사이클 없음). `GrantLevelUpRewardsAsync` 내부 `GrantAsync` self-recurse는 Exp=0 번들만 사용하므로 반드시 종료됨
- 호출 경로: `StageClearService.CompleteAsync`, `MailService.ClaimAsync` 등에서 `IExpService.AddExpAsync` → 반환 레벨 목록 → `IRewardDispatcher.GrantLevelUpRewardsAsync` 순서로 처리

### 인증 예외 계층 — AuthDomainException (2026-05-07)

`UnauthorizedAccessException` 대신 도메인 전용 예외 계층을 도입.

| 예외 클래스 | ErrorCode | 발생 위치 |
|---|---|---|
| `PlayerBannedException` | `AUTH_BANNED` | GuestLogin·Refresh·GoogleLogin 밴 체크 |
| `InvalidRefreshTokenException` | `AUTH_TOKEN_INVALID` | Refresh — 토큰 DB 미조회 |
| `RefreshTokenRevokedException` | `AUTH_TOKEN_REVOKED` | Refresh — 명시적 폐기 토큰 |
| `RefreshTokenExpiredException` | `AUTH_TOKEN_EXPIRED` | Refresh — 만료 토큰 |
| `InvalidGoogleTokenException` | `AUTH_GOOGLE_TOKEN_INVALID` | GoogleTokenVerifier 검증 실패 |

- **선택 근거**: .NET 프레임워크의 `UnauthorizedAccessException`(파일 권한 등)과 도메인 인증 실패를 분리. `catch (PlayerBannedException)`으로 GuestLogin의 403 반환 의도를 타입 수준에서 명시. errorCode 세분화로 Unity 클라이언트 분기 처리 지원.
- **위치**: `Framework.Application/Features/Auth/Exceptions/` (Application 레이어 — Api → Application 의존 방향 정상)
- **핸들러**: `AuthDomainExceptionHandler` — `IProblemDetailsService.WriteAsync` 미사용, `JsonSerializer`로 직접 응답 직렬화. Development 환경 크래쉬 방지(`ProblemDetailsService` 내부 실패 시 핸들러 자체 예외 전파 차단). GuestLogin은 `PlayerBannedException`을 컨트롤러 로컬 catch로 403 반환(Unity 클라이언트가 401을 token refresh 트리거로 해석 → 로그인 화면에서 SessionExpired 오발생 방지).
- **개발 환경 주의**: 테스트 프로젝트를 F5(디버그 모드)로 실행하면 `Assert.ThrowsAsync`가 잡기 전에 VS 디버거가 예외에서 중단함. 테스트는 Test Explorer로만 실행할 것.

### auth Rate Limit — IP/PlayerId 이중 파티션 (round_20260503, C-2)

`AddPolicy("auth", httpContext => ...)` 동적 파티션 키 — 인증 시 `player:{id}` / 미인증 시 `ip:{remote}`로 분기. `game`/`iap-verify` 정책과 동일 패턴.

| 파티션 | 한도 | 설정 키 | 의도 |
|---|---|---|---|
| 미인증 IP | 분당 15회 | `RateLimiting:AuthPermitLimit` | 게스트 로그인/구글 로그인 폭주 차단 |
| 인증 PlayerId | 분당 30회 | `RateLimiting:AuthPlayerPermitLimit` | refresh/logout 정상 사용 보장 |

- **선택 근거**: 단일 글로벌 limiter(C-2 발견 당시)는 모든 IP·유저 합산 분당 60회 → 단일 공격자가 정상 사용자 인증 차단 가능. 파티션 분기로 격리.
- **잔여 추적**: ForwardedHeaders / docker bridge IP 보존 — Caddy 도입 시점에 별도 라운드.

### RefreshToken — DB 평문 저장 금지, SHA-256 해시 (round_20260503, H-6)

DB에는 `TokenHash`(SHA-256/Base64) + 보안 메타데이터(`IpAddress`/`UserAgent`/`RotatedFromId`/`RevokedAt`)만 저장. 원문 토큰은 클라이언트만 보유.

- **검증 흐름**: Refresh 요청 시 `JwtTokenProvider.ComputeRefreshTokenHash(rawToken)`으로 해시 계산 → DB 비교
- **선택 근거**: DB 유출 시 평문 RefreshToken 노출 차단. AccessToken과 달리 RefreshToken은 만료까지 장기 유효(7일 등)라 평문 저장 위험 큼
- **마이그레이션**: `H6_RefreshTokenHashAndSecurityMetadata` (dev 환경 토큰 DELETE FROM 포함)
- **잔여**: `RotatedFromId`/`RevokedAt` 활용 로직(강제 로그아웃·회전 추적) — 별도 라운드

### Strategy 패턴 — IAP/광고 검증기

| 결정 | 근거 |
|---|---|
| 스토어별/네트워크별 검증 로직을 `IIapStoreVerifier` / `IAdNetworkVerifier` 인터페이스로 추상화 | 향후 Apple Store, AdMob 등 추가 시 검증기 클래스 1개 + DI 등록 1줄로 확장 |
| Resolver 서비스가 런타임에 스토어/네트워크 키로 검증기 선택 | Controller → Resolver → 검증기 흐름 유지, 분기 로직 Controller 노출 방지 |

현재 구현: Google Play(`GooglePlayStoreVerifier`), Unity Ads(`UnityAdsVerifier`), IronSource(`IronSourceVerifier`).
미구현 예약: Apple(`AppleStoreVerifier`), AdMob 등.

### Content 영역 분리

`Framework.Application/Content/`, `Framework.Domain/Content/` 폴더에 게임 컨텐츠(스테이지 등) 코드 배치.
- **규칙**: Framework 영역(Application/Domain/Infrastructure 비-Content 경로)에서 Content 영역 직접 참조 금지. Content → Framework 방향만 허용.
- **근거**: 게임 컨텐츠 교체/대규모 변경 시 Framework 코어에 영향 없도록 의존 방향 제한. 현재는 폴더 컨벤션 수준 강제(컴파일러 강제 없음 — L-4 참조).

### 일일 보상 Current/Next 2슬롯 방식

`DailyRewardSlots` 테이블에 `(Slot, Day)` 복합 PK로 최대 56행 고정 (Slot: 1=이번달·2=다음달, Day: 1~28).
- **운영**: 이번 달 보상 편집 중에도 다음 달 보상을 미리 등록 가능. 월말에 Slot 전환만 수행.
- **근거**: 월 경계 운영 부담 분산. 달력 계산 단순화 — 날짜 범위 대신 로그인 횟수(cycleDay)로 보상 결정.

### 낙관적 동시성 토큰 — PostgreSQL `xmin` 채택

PlayerItem.Quantity / Mail.IsClaimed / IapPurchase.Status 등 동시 갱신이 발생할 수 있는 엔티티에 PostgreSQL 시스템 컬럼 `xmin`을 그림자 속성으로 매핑하여 낙관적 동시성 토큰으로 사용.

- **선택 근거**: 명시적 RowVersion 컬럼(B안) 대비 마이그레이션 부담 0(시스템 컬럼이라 DDL 불필요), 기존 `Mail.IsClaimed` 패턴과 일관, Domain 엔티티에 RowVersion 필드를 추가하지 않아 도메인 오염 없음.
- **트레이드오프**: PostgreSQL 의존성 발생 — 향후 다른 RDB로 전환 시 명시적 RowVersion 컬럼 + 백필로 마이그 필요. 현 단계에서는 PostgreSQL 영구 채택 방침으로 수용.

#### `xmin` 마이그레이션 표준 절차

마이그레이션 추가 절차(4단계)는 `SERVER_GUIDE.md` §5.2 참조. EF Core가 자동 생성하는 `AddColumn<uint>("xmin", ...)`은 PostgreSQL 시스템 컬럼 충돌로 거부되므로 **`Up()`/`Down()` 본문 수동 비우기가 필수**다.

> **[Admin 경로 추가 시 의무]** 향후 Admin에서 IapPurchase.Status를 변경하는 경로(강제 환불, 상태 조정 등)를 추가할 경우 동일한 xmin 동시성 토큰 처리가 의무이다. 단, 운영자 액션 충돌은 재시도 없이 명시적 에러를 노출하여 운영자가 최신 상태를 확인하고 재작업하도록 유도할 것.

#### 재시도 정책

- 호출자(Service) 직접 책임 — `RewardDispatcher.GrantAsync`, `MailService.ClaimAsync` 등이 `ExecuteInTransactionAsync` 외부에 retry loop 배치
- `DbUpdateConcurrencyException` catch 시 `IUnitOfWork.ClearChangeTracker()` 호출 후 재시도
- 최대 3회, 백오프 없음 (단일 행 contention은 ms 단위로 수렴)
- MailService는 `ex.Entries[0].Entity` 타입 검사로 `Mail.IsClaimed` 충돌(즉시 false 반환, 재시도 무의미) vs `PlayerItem.xmin` 충돌(재시도) 구분

## [기술 부채] 검토 항목
- **일괄 우편 발송 성능** — `MailService.BulkSendAsync`가 전체 플레이어를 메모리 로드 후 단일 트랜잭션으로 N건 INSERT. 유저 수 증가 시 메모리 압박 + DB 락 시간 문제 발생. 배치 분할(500건씩 끊어서 INSERT + SaveChanges) 도입 필요
- **DiSmokeTests ValidateOnBuild 미적용** — `DiSmokeTests`가 `services.BuildServiceProvider()`를 `ValidateOnBuild`/`ValidateScopes` 없이 호출 + `AddContentServices()` 미호출로 DI 사이클이 형성되지 않아 정적 검증 불가. 처치: `ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }` 적용 + 모든 `Add*Services`/`Add*Repositories` 호출 보강.

### REVIEW_REPORT.md 우선순위 처리 결과 (round_20260503 종결: 2026-05-05)

- **Critical** (2/2) 모두 해결: C-1 Admin 인가 누락, C-2 auth Rate Limit 파티션
- **High** (12/14 해결 + 5건 의도적 보류): H-1~H-4, H-6~H-8, H-10, H-12 해결. H-5/H-9/H-11/H-14/H-15는 출시·운영 단계 도입 사유로 박제 보류 (REVIEW_REPORT 5장 참조)
- **Medium** 다수 해결: M-2/M-5/M-11/M-13/M-15/M-17/M-18/M-19/M-20~M-23/M-24/M-29/M-37/M-38/M-40/M-44/M-46/M-48~M-50
- **Low** 일부 해결: L-2/L-3/L-15/L-17/L-18/L-23/L-44

잔여 Med/Low는 출시 후 운영 데이터 기반 우선순위 재평가 — REVIEW_REPORT.md 6/7장 참조.


## [미구현] 추가 개발 필요 항목
- **계정 탈퇴 유예 기간(취소 흐름)** — 현재는 즉시 익명화 처리. 사용자 실수 탈퇴 복구 불가. 향후 출시 후 민원 패턴 따라 BackgroundService로 N일 유예 검토. 도입 시: `WithdrawalScheduledAt` 컬럼 + BackgroundService 1개 + 취소 API 1개. Unity 클라이언트 안내 팝업도 같이.
- **외부 호출 타임아웃 미설정** — `GoogleTokenVerifier` Google 토큰 검증 HTTP 호출에 명시적 타임아웃 없음. 응답 지연 시 스레드풀 고갈 위험. `HttpClient.Timeout` 또는 `CancellationToken` with deadline 설정 필요
- **일시적 DB 오류 재시도 없음** — xmin 동시성 재시도 외 네트워크 순단 등 일시적 연결 실패는 즉시 500 반환. Polly `WaitAndRetryAsync`(지수 백오프, 최대 3회) — 멱등 엔드포인트 우선 적용
- **PII 정리 BackgroundService HealthCheck 통합** — 현재는 try/catch 재시도만. 정지 시 보관기간 위반 위험. IHealthCheck 등록 + 마지막 성공 시각 30시간 초과 시 Unhealthy + AdminNotification 등록 권고
- **PII 정리 BackgroundService 다중 인스턴스 안전성** — PostgreSQL advisory lock(`pg_try_advisory_lock`)으로 동시 실행 방지. 단일 컨테이너 운영 시 불필요
- **DailyLogin 보상 누락 자동 보전 워커** — 2차 트랜잭션(RewardDispatcher 호출) 실패 시 현재는 AdminNotification + 운영자 수동 처리. 자동 보전 워커는 별도 라운드
- **Apple StoreKit 영수증 검증** — `IapStore.Apple(=2)` Enum 예약 + `AppleStoreVerifier` 미구현 (※ Apple IAP 검증기 항목과 동일, 플랫폼 출시 시점에 도입)

- **공지사항 페이지** [선택] — 현재는 1회성 텍스트 공지만 구현됨. 공지 이력 열람, 카테고리 분류 등 게시판 형태가 필요해지면 별도 페이지 추가 고려
- **백업 정책** — DB 백업은 애플리케이션 관할 아님. Docker로 운영 중인 PostgreSQL 컨테이너/볼륨 레벨에서 별도 설정 필요 (pg_dump, 볼륨 스냅샷 등). 최소 1일 1회 백업, 30일 보관 권장
- **Apple IAP 검증기** — `IapStore.Apple(=2)` Enum은 예약되어 있으나 `AppleStoreVerifier` 구현체 미존재. Apple 플랫폼 출시 시 추가 필요
- **이벤트 기간 관리** [중요도 낮음] — 기간 한정 이벤트 시작/종료 관리. 클라이언트가 현재 이벤트 진행 여부를 서버에 질의. 게임마다 구조가 달라 범용 설계 필요
- **로그/APM 도구 연동** [중요도 낮음] — 현재 파일 로그(Serilog) 기반. 유저 증가 시 ELK Stack + Elastic APM 연동 권장 (APM이 ELK 위에서 동작하므로 세트로 도입). 가벼운 대안으로 Seq(컨테이너 1개, .NET 친화적) 또는 Grafana+Loki 가능. Serilog 싱크 추가 + Program.cs 한 줄로 연동 가능
- **SignalR 허브 Rate Limiting** — `/hubs/matchmaking` 등 SignalR 허브 연결에 대한 Rate Limiting 미구현. HTTP 요청과 달리 앱 백그라운드/포그라운드 전환 시 재연결이 발생해 game 정책 직접 적용 불가. 별도 설계 필요. 구현 시점: 실 서비스 직전 또는 연결 폭주 사례 발생 시
- **계정 탈퇴 안내 UI (Unity 클라이언트)** — 백엔드 탈퇴 처리(`DELETE /api/auth/withdraw`)와 별개로 클라이언트 탈퇴 화면에 안내 팝업 필수. 법적/마켓 정책 의무 항목·구현 요구사항은 `CLIENT_GUIDE.md` 10번 + 부록 A 참조

## 관련 문서

- **`SERVER_GUIDE.md`** — 서버 운영자/개발자 절차 (시크릿 교체, 마이그레이션, SystemConfig, Test 가이드, 배포 체크리스트, 이슈 대응)
- **`CLIENT_GUIDE.md`** — Unity 클라이언트 구현 walkthrough (앱 부팅~결제 순차 가이드)
- **`REVIEW_REPORT.md`** — 리뷰 라운드 보고서 (Critical/High/Med/Low 처리 내역)
- **`CLAUDE.md`** — 코드 컨벤션 (한국어 주석 규칙, 협업 프로토콜)
