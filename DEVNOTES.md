# Dev Notes

## Feature Status

| 기능 | 설명 |
|---|---|
| JWT 인증 | 게스트 로그인(DeviceId), AccessToken/RefreshToken 발급, 로그아웃 |
| 구글 OAuth 연동 | Google IdToken 검증, 신규 로그인 및 기존 계정 연결, 계정 충돌 감지(409)/해소, 게스트 계정 소프트 딜리트 |
| 게스트 결제 차단 | `[RequireLinkedAccount]` 필터 — 구글 미연동 계정의 결제 엔드포인트 접근 시 403 반환 (결제 컨트롤러 구현 시 부착 필요) |
| 랭킹 시스템 | 게임 결과 점수 기록, 상위 N명 랭킹 조회 |
| 인벤토리 관리 | 플레이어 보유 아이템 조회, 아이템 획득. TODO: 인벤토리 조회 응답에 아이템 외 기본 재화(Gold, Gems 등 PlayerProfile 컬럼)도 함께 표기 고려 |
| 우편 시스템 | 우편 수신/수령 API, Admin 단건·일괄 발송 |
| 일일 로그인 보상 | 로그인 시 당일 보상 우편 발송 (이번 달 로그인 횟수 기반, 매월 리셋). 빈 일자는 보상 없음. Current/Next 2슬롯 방식으로 이번 달·다음 달 보상 예약 관리. KST 하루 기준 시각(기본 00:00) Admin 설정 가능 |
| 매치메이킹 | SignalR 기반 실시간 매칭, 대기열 관리 |
| 보상 프레임워크 | 범용 보상 파이프라인 — 모든 보상 경로를 단일 IRewardDispatcher로 통합. 선기록 멱등성, Direct/Mail/Auto 분기, RewardTable 마스터 관리, Admin 수동 지급/우편 발송 통합 페이지 |
| 아이템 마스터 관리 | Admin CRUD (추가/수정/소프트삭제), 보유 유저 수 확인 |
| Admin 인증 | X-Admin-Key 헤더 기반 API 접근 제어. Admin 컨트롤러는 `[AdminApiKey]` 필터로 보호 (JWT [Authorize]와 독립). 점검 미들웨어에서 X-Admin-Key 확인 시 503 면제. Admin 로그인 비밀번호는 BCrypt 해시 검증(`AdminPasswordVerifier`, `Admin:PasswordHash` 키) |
| 시스템 설정 | 점검 모드, 앱 버전, 일일 보상 기준 시각 등 SystemConfig Admin 제어 |
| 어뷰징 방어 | auth 엔드포인트 Rate Limiting (IP 기준, `RateLimiting:AuthPermitLimit` 설정), 429 발생 시 PlayerId·UserAgent 포함 DB 로그. Admin 보안 감시 — 통합 타임라인(Rate Limit 초과 / 재화 이상치 / 계정 정지 이벤트 병합), IP 집계, 타임라인에서 직접 영구밴 가능. 인게임 API: `game` 정책 PlayerId 기준 120회/분, IAP 검증: `iap-verify` 정책 20회/분 |
| 점검 모드 | 수동 ON/OFF 및 시각 예약, 미들웨어에서 503 차단, Admin은 점검 중에도 접근 가능 |
| 계정 탈퇴 | DELETE /auth/withdraw, 플레이어 즉시 하드 삭제, CASCADE로 모든 연관 데이터 삭제 (개인정보보호법 제21조 준수) |
| 클라이언트 앱 버전 체크 | GET /api/version/check, 강제 업데이트 여부 반환, Admin에서 최소/최신 버전 설정 (서버 버전 아님 — 앱스토어 배포 Unity 빌드 기준) |
| 공지/1회 공지 시스템 | **공지**: `GET /api/notices/latest` 최신 활성 공지 1개 반환. 클라이언트가 NoticeId를 PlayerPrefs에 저장해 1회성 팝업 표시. Admin CRUD. **1회 공지**: Admin에서 전체/특정 플레이어 대상 HUD 텍스트 발송. DB 이력 기록. 클라이언트 접속 시 `GET /api/shouts/active` 1회 호출(폴링 방식) — 만료 시간 내 활성 1회 공지 수신. Admin `/notices`, `/shouts` 페이지 별도 관리 |
| 플레이어 문의 | POST /api/inquiries 제출, GET /api/inquiries 내 목록 조회. Admin 답변 등록. 소원수리함 형태(자유 텍스트). Blazor 테스트 페이지 포함 |
| 감사 로그 | 재화/아이템 변동 추적. Item.AuditLevel(AnomalyOnly/Full) + AnomalyThreshold 기준으로 저장 범위 차별화. Admin `/audit-logs` 페이지에서 플레이어·아이템·기간·이상치 필터 조회. 현재 훅은 `MailService.ClaimAsync` 적용. **[미구현] Currency(Gold/Gems/Exp) 로그 누락** — `AuditLog.ItemId`가 non-nullable int라 Currency 기록 불가. 구조 개선 전까지 우편 수령 시 Currency 변동은 감사 로그 미기록 |
| 광고 SSV 보상 | Unity Ads / IronSource SSV(Server Side Verification) 콜백 검증 및 보상 지급. Strategy 패턴으로 모듈화 — 새 네트워크 추가 시 검증기 클래스 1개 + DI 등록 1줄. HMAC-SHA256 서명 검증, 일일 한도 제한, RewardDispatcher 멱등성 보장. Admin `/ad-policies` 페이지에서 PlacementId별 보상 정책 CRUD 관리. 콜백 URL: `GET /api/ads/callback/unity-ads`, `GET /api/ads/callback/ironsource` |
| 트랜잭션 추상화 | `IUnitOfWork` 인터페이스(Domain) + `UnitOfWork` 구현체(Infrastructure). RewardDispatcher가 IUnitOfWork를 통해 전체 보상 지급을 단일 트랜잭션으로 보장 |
| 인앱 결제(IAP) | Google Play 영수증 서버 검증 및 보상 지급. Strategy 패턴으로 스토어별 모듈화(현재 Google Play 구현, Apple 예약). OIDC 기반 RTDN(환불 알림) 수신 및 자동 환불 처리. Admin `/iap-products` 상품 관리, `/iap-purchases` 구매 이력 조회. API: `POST /api/iap/google/verify`, `POST /api/iap/google/rtdn`. Rate Limit: iap-rtdn 600회/분 |
| 레벨/경험치 | `IExpService` — Exp 누적, 임계값 초과 시 자동 레벨업 + 레벨업 보상 지급(`SourceKey="levelup:{level}"`). 다중 레벨업 while 루프. 임계값은 `LevelThresholds` DB 테이블로 외부화 — Admin `/level-thresholds` 페이지에서 CRUD 관리, `ILevelTableProvider`(Singleton 캐시) 통해 런타임 조회 |
| 스테이지 클리어 [컨텐츠] | `POST /api/stages/{stageId}/complete`. 순차 진행 조건(`RequiredPrevStageId`), 최초 클리어 보상 + 재클리어 보상 감소(decay%), Exp/레벨업 연동. Admin 스테이지 마스터 CRUD. **`Content/` 영역 분리** — 게임 컨텐츠 코드, Framework 영역에서 참조 금지 |
| 운영 알림(AdminNotification) | RTDN 환불 등 운영 이슈를 Admin에 즉시 통지. `AdminNotification` 엔티티 + Repository/Service 전 레이어 구현. API: `GET /api/admin/notifications/unread-count`, `GET /api/admin/notifications`, `POST /:id/read`, `POST /:id/unread`, `POST /read-all`. Admin UI: 헤더 `NotificationBell`(30초 폴링), `/admin-notifications` 페이지(필터/페이지네이션/읽음토글). RTDN 환불 시 자동 생성 — `Voided=Critical`, `Canceled=Warning` |

---

## [필수] Framework.Api/appsettings.json 교체값
라이브 배포 전 .env 파일에 반드시 실제 값을 채워야 하는 항목 목록입니다.

| 키 | .env 변수명 | 교체 방법 |
|---|---|---|
| `Jwt:SecretKey` | `JWT_SECRET_KEY` | 32자 이상 랜덤 문자열로 교체 |
| `Admin:ApiKey` | `ADMIN_API_KEY` | 랜덤 문자열로 교체 |
| `Google:ClientId` | `GOOGLE_CLIENT_ID` | Google Cloud Console에서 OAuth 클라이언트 ID 발급 |
| `ConnectionStrings:Default` | `POSTGRES_PASSWORD` | 운영 DB 비밀번호 설정 |
| `AdNetworks:UnityAds:SecretKey` | `UNITY_ADS_SECRET_KEY` | Unity Ads 대시보드 > 수익화 > 광고 > SSV 설정에서 발급 |
| `AdNetworks:IronSource:SecretKey` | `IRONSOURCE_SECRET_KEY` | IronSource 대시보드 > SDK 네트워크 > 고급 설정에서 발급 |
| `Iap:Google:PackageName` | `IAP_GOOGLE_PACKAGE_NAME` | 실제 앱 패키지명 (예: com.yourcompany.yourgame) |
| `Iap:Google:ServiceAccountJsonPath` | `IAP_GOOGLE_SERVICE_ACCOUNT_JSON_PATH` | Google Play 서비스 계정 JSON 파일 경로 (Git 커밋 금지) |
| `Iap:Google:RtdnAudience` | `IAP_GOOGLE_RTDN_AUDIENCE` | RTDN Push subscription 수신 URL (예: https://api.yourdomain.com/api/iap/google/rtdn) |

> `secrets/google-play-service-account.json` — Google Cloud Console에서 발급한 서비스 계정 JSON. 절대 Git에 커밋하지 말 것 (.gitignore 확인)

## [필수] Google Play 연동 준비 사항
라이브 배포 전 Google Cloud / Play Console 설정이 필요합니다.

1. **서비스 계정 생성** — Google Cloud Console > IAM > 서비스 계정 생성 후 JSON 키 발급
2. **Play Console 권한 부여** — Play Console > 설정 > API 액세스 > 서비스 계정에 "주문 관리" 권한 부여
3. **Pub/Sub 설정** — Google Cloud Pub/Sub > Topic 생성 + Push subscription 대상을 `/api/iap/google/rtdn`으로 설정
4. **License Testers 등록** — Play Console > 설정 > License Testers에 테스트 계정 등록 (테스트 환경)

## [필수] Framework.Admin/appsettings.json 교체값
- `ApiBaseUrl` — 현재 `https://api.overture.io.kr`. 도메인 변경 시 교체 필요
- `Admin:PasswordHash` (.env 변수명: `Admin__PasswordHash`) — BCrypt 해시값으로 설정. 해시 생성 방법: `dotnet run --project Framework.Admin -- --hash "비밀번호"` 실행 후 출력값을 복사
  - **[보안 주의]** 위 명령은 평문 비밀번호가 셸 히스토리/프로세스 목록에 기록됨. 해시 생성 후 셸 히스토리 삭제(`history -c` 또는 PowerShell `Clear-History`) 또는 격리된 1회용 환경에서 실행 권장
- `Admin:ApiKey` — Framework.Api의 `.env` `ADMIN_API_KEY`와 동일한 값으로 설정 필요

## [성능] DB 인덱스 미적용 항목
현재 적용된 인덱스는 데이터 무결성(유니크 제약) 목적의 필수 인덱스만 존재합니다.
성능용 세컨더리 인덱스는 의도적으로 추가하지 않았습니다 — 유저 수가 늘어날 때 적용 전/후 성능 비교 후 추가 권장합니다.
인덱스 추가 위치: `Framework.Infrastructure/Persistence/AppDbContext.cs` `OnModelCreating()`

| 테이블 | 컬럼 | 사용 쿼리 | 예상 효과 |
|---|---|---|---|
| `Mails` | `PlayerId` + `IsClaimed` | 미수령 우편 조회 | 우편함 조회 속도 개선 |
| `Mails` | `ExpiresAt` | 만료 우편 정리 | 만료 처리 속도 개선 |


## [SystemConfig] 전체 키 목록
`SystemConfig` 테이블에 저장되는 모든 키 (`Framework.Domain/Constants/SystemConfigKeys.cs`). Admin 시스템 설정 페이지에서 관리 가능.

### 점검 모드
| 키 | 기본값 | 설명 |
|---|---|---|
| `maintenance_mode` | `"false"` | 점검 수동 강제 ON/OFF (`"true"`/`"false"`) |
| `maintenance_start_at` | `""` | 점검 예약 시작 시각 (ISO 8601 UTC, 빈값이면 미예약) |
| `maintenance_end_at` | `""` | 점검 예약 종료 시각 (ISO 8601 UTC, 빈값이면 미예약) |

### 앱 버전
| 키 | 기본값 | 설명 |
|---|---|---|
| `client_app_min_version` | `""` | 강제 업데이트 기준 최소 버전 — 이 버전 미만 클라이언트는 업데이트 강제. 서버 버전과 무관, 앱스토어 배포 Unity 빌드 기준 |
| `client_app_latest_version` | `""` | 현재 최신 앱 버전 — 소프트 업데이트 안내용 |

### 일일 보상
| 키 | 기본값 | 설명 |
|---|---|---|
| `daily_reward_active_month` | `"202604"` | 현재 활성 연월 (YYYYMM) — 월 전환 감지용, 자동 갱신 |
| `daily_reward_day_boundary_hour_kst` | `"0"` | 하루 기준 시각 KST 시(0~23) |
| `daily_reward_day_boundary_minute_kst` | `"0"` | 하루 기준 시각 KST 분(0~59) |
| `daily_reward_default_item_id` | `""` (미설정) | 월 28회 초과 시 지급할 기본 보상 아이템 ID. 빈값이면 보상 미발송 |
| `daily_reward_default_item_count` | `"0"` | 기본 보상 아이템 수량. 0이면 보상 미발송 |

기준 시각(00:00 기본) 미만이면 전날 날짜로 게임 날짜 계산.
cycleDay는 이번 달 로그인 횟수 기반 (1번째 로그인 = Day 1, 28번째 = Day 28, 29번째 이후 = 기본 보상).

## [설계 결정]

### 보상 프레임워크

#### 확정 사항

| # | 결정 | 근거 |
|---|---|---|
| 1 | Gold/Gems/Exp — PlayerProfile 컬럼 유지, Item 마스터는 정의(이름·아이콘)용만 | 아이템 마스터는 조회용, 보유량은 PlayerProfile이 정원 |
| 2 | MailItems 테이블 도입 — 1통에 N종 아이템 묶음 발송 | 다중 보상 UX·트랜잭션 일관성 |
| 3 | PlayerRecord 폐기 → GameResultParticipants로 대체 | 랭킹 데이터 미존재, 매치 식별자 없는 기존 구조 한계 |
| 4 | DailyLoginLog + RewardGrants 이중보호 유지 | DailyLoginLog는 로그인 통계 겸용, RewardGrants는 범용 멱등성 |

#### 공통 파이프라인

```
[호출자: 빈 번들·PlayerId 검증]
  → [UnitOfWork 트랜잭션 진입]
  → [RewardGrant 선기록 + UNIQUE 위반 catch → Duplicate 반환]
  → [DetermineMode: Auto면 IsCurrencyOnly로 Direct/Mail 자동 결정]
  → [Direct: PlayerProfile.Gold/Gems/Exp 가산 + PlayerItem 추가]
    또는
   [Mail: Mail + MailItems 생성 → 수령 시 ClaimAsync에서 실지급]
  → [Mail 모드면 grant.MailId 업데이트]
  → [트랜잭션 커밋 + 결과 반환]
```
※ Source별 권한/사전 검증은 호출자(Service) 책임. RewardDispatcher는 멱등성·지급·트랜잭션만 담당
※ AuditLog 기록은 현재 `MailService.ClaimAsync`에서만 수행 (RewardDispatcher 내 미구현, [미구현] 섹션 참조)

#### 레벨업 처리

- Exp는 PlayerProfile 컬럼 유지 (Item화하지 않음)
- `IExpService` (위치: `Framework.Application/Features/Exp/`)가 Exp 누적 → 임계값 초과 시 Level 증가 → 레벨업 보상을 RewardDispatcher로 위임
- 레벨 임계값은 `LevelThresholds` 테이블 + `ILevelTableProvider` Singleton 캐시
- 레벨업 보상은 `RewardTable`에서 `Code="levelup:{level}"`로 조회, `SourceKey="levelup:{level}"`로 멱등 보장
- 다중 레벨업 while 루프 지원 (한 번의 AddExp 호출로 여러 레벨 가능)
- 호출 경로: `MailService.ClaimAsync`(우편 Exp 수령) 등에서 `IExpService.AddExpAsync` 호출

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

## [기술 부채] 검토 항목
- **Admin 컨트롤러 익명 객체 응답** — 일부 Admin 컨트롤러가 DTO 없이 익명 객체로 응답을 구성함. Admin 전용 단순 조회라 즉각 위험은 낮으나, 신규 Admin 기능 구현 시에는 DTO 정의 원칙 준수 필요
- **일괄 우편 발송 성능** — `MailService.BulkSendAsync`가 전체 플레이어를 메모리 로드 후 단일 트랜잭션으로 N건 INSERT. 유저 수 증가 시 메모리 압박 + DB 락 시간 문제 발생. 배치 분할(500건씩 끊어서 INSERT + SaveChanges) 도입 필요

### REVIEW_REPORT.md 우선순위 처리 결과
우선순위 목록(C-1, C-2, H-1~H-6, H-9, M-8~M-12, M-14) 모두 완료. Medium/Low 백로그만 잔여 — REVIEW_REPORT.md 참조.


## [미구현] 추가 개발 필요 항목
- **공지사항 페이지** [선택] — 현재는 1회성 텍스트 공지만 구현됨. 공지 이력 열람, 카테고리 분류 등 게시판 형태가 필요해지면 별도 페이지 추가 고려
- **감사 로그 훅 확장** — 현재는 `MailService.ClaimAsync`에만 훅 적용됨. 상점 구매/스테이지 보상/Admin 직접 지급 등 기능 구현 시 `IAuditLogService.RecordAsync` 호출 추가 필요
- **감사 로그 Currency 구조 개선** — `AuditLog.ItemId`가 non-nullable int라 Gold/Gems/Exp 같은 Currency 변동을 기록할 수 없음. 개선 방향: `ItemId` nullable 전환 + `CurrencyType` 컬럼 추가(Gold/Gems/Exp enum), 또는 Currency 전용 별도 로그 테이블 분리. 현재는 `MailService.ClaimAsync`에 TODO 주석 기재(`MailService.cs:197`)
- **밴/밴해제 로그** — `AdminPlayersController`의 Ban/Unban 엔드포인트 처리 후 별도 로그 기록 필요. 누가(Admin), 언제, 어떤 플레이어를 밴/해제했는지 감사 추적이 현재 없음. AuditLog 또는 전용 BanLog 테이블 중 택일하여 구현 필요
- **백업 정책** — DB 백업은 애플리케이션 관할 아님. Docker로 운영 중인 PostgreSQL 컨테이너/볼륨 레벨에서 별도 설정 필요 (pg_dump, 볼륨 스냅샷 등). 최소 1일 1회 백업, 30일 보관 권장
- **IAP Consumable consume API 호출** — `GooglePlayStoreVerifier`에서 소모성 상품 검증 후 Google Play `purchases.products.consume` API 호출이 TODO 상태로 남아있음. 미호출 시 동일 purchaseToken으로 재구매 불가. 구현 위치: `Framework.Infrastructure/Iap/GooglePlayStoreVerifier.cs`
- **Apple IAP 검증기** — `IapStore.Apple(=2)` Enum은 예약되어 있으나 `AppleStoreVerifier` 구현체 미존재. Apple 플랫폼 출시 시 추가 필요
- **이벤트 기간 관리** [중요도 낮음] — 기간 한정 이벤트 시작/종료 관리. 클라이언트가 현재 이벤트 진행 여부를 서버에 질의. 게임마다 구조가 달라 범용 설계 필요
- **로그/APM 도구 연동** [중요도 낮음] — 현재 파일 로그(Serilog) 기반. 유저 증가 시 ELK Stack + Elastic APM 연동 권장 (APM이 ELK 위에서 동작하므로 세트로 도입). 가벼운 대안으로 Seq(컨테이너 1개, .NET 친화적) 또는 Grafana+Loki 가능. Serilog 싱크 추가 + Program.cs 한 줄로 연동 가능
- **SignalR 허브 Rate Limiting** — `/hubs/matchmaking` 등 SignalR 허브 연결에 대한 Rate Limiting 미구현. HTTP 요청과 달리 앱 백그라운드/포그라운드 전환 시 재연결이 발생해 game 정책 직접 적용 불가. 별도 설계 필요. 구현 시점: 실 서비스 직전 또는 연결 폭주 사례 발생 시
