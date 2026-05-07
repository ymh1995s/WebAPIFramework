# SERVER_GUIDE — 서버 운영자/개발자 가이드

이 문서는 Web API Framework 백엔드를 **운영·배포·유지보수**하는 서버 개발자를 위한 절차 중심 가이드다.

- **청중**: 서버 운영자, 백엔드 개발자, DevOps
- **선행 문서**: 설계 의도/박제는 `DEVNOTES.md` 참조. 클라이언트 구현은 `CLIENT_GUIDE.md` 참조
- **사용법**: 신규 환경 셋업 → 시크릿 교체 → 마이그레이션 → 배포 순서로 따라가기

---

## 1. 신규 환경 셋업 체크리스트

### 1.1 사전 요건
- .NET 10 SDK
- PostgreSQL 16+ (또는 docker-compose 포함 인스턴스)
- Docker Desktop (선택 — 컨테이너 운영 시)
- EF Core 도구: `dotnet tool install --global dotnet-ef`

### 1.2 첫 실행 절차
```
1. git clone + cd WebAPIFramework
2. .env 파일 작성 (.env.example 복사 후 시크릿 채우기 — §2 참조)
3. dotnet restore
4. dotnet build Framework.Api.slnx
5. (DB 컨테이너 사용 시) docker-compose up -d postgres
6. dotnet ef database update --project Framework/Framework.Infrastructure --startup-project Framework/Framework.Api
7. dotnet run --project Framework/Framework.Api
8. 별도 터미널: dotnet run --project Framework/Framework.Admin (Blazor Admin)
```

### 1.3 동작 확인
- API: `http://localhost:5000/health` → 200 + `{"status":"Healthy",...}`
- API Swagger: `http://localhost:5000/swagger` (Development 환경만)
- Admin: `http://localhost:5001` (DEBUG 빌드 시 자동 로그인)

---

## 2. [필수] Framework.Api 시크릿 교체값

라이브 배포 전 `.env` 파일에 반드시 실제 값을 채워야 하는 항목.

| 키 | .env 변수명 | 교체 방법 |
|---|---|---|
| `Jwt:SecretKey` | `JWT_SECRET_KEY` | 32자 이상 랜덤 문자열로 교체 |
| `Admin:ApiKey` | `ADMIN_API_KEY` | 랜덤 문자열로 교체 |
| `Google:ClientId` | `GOOGLE_CLIENT_ID` | Google Cloud Console에서 OAuth 클라이언트 ID 발급 |
| `ConnectionStrings:Default` | `POSTGRES_PASSWORD` | 운영 DB 비밀번호 설정 |
| `AdNetworks:UnityAds:SecretKey` | `UNITY_ADS_SECRET_KEY` | Unity Ads 대시보드 > 수익화 > 광고 > SSV 설정에서 발급 |
| `AdNetworks:IronSource:SecretKey` | `IRONSOURCE_SECRET_KEY` | IronSource 대시보드 > SDK 네트워크 > 고급 설정에서 발급 |
| `Iap:Google:PackageName` | `IAP_GOOGLE_PACKAGE_NAME` | 실제 앱 패키지명 (예: `com.yourcompany.yourgame`) |
| `Iap:Google:ServiceAccountJsonPath` | `IAP_GOOGLE_SERVICE_ACCOUNT_JSON_PATH` | Google Play 서비스 계정 JSON 파일 경로 (Git 커밋 금지) |
| `Iap:Google:RtdnAudience` | `IAP_GOOGLE_RTDN_AUDIENCE` | RTDN Push subscription 수신 URL (예: `https://api.yourdomain.com/api/iap/google/rtdn`) |

> `secrets/google-play-service-account.json` — Google Cloud Console에서 발급한 서비스 계정 JSON. **절대 Git에 커밋하지 말 것** (.gitignore 확인)

---

## 3. [필수] Framework.Admin 시크릿 교체값

| 키 | .env 변수명 | 교체 방법 |
|---|---|---|
| `ApiBaseUrl` | - | 현재 `https://api.overture.io.kr`. 도메인 변경 시 교체 필요 |
| `Admin:PasswordHash` | `Admin__PasswordHash` | BCrypt 해시값. 생성: `dotnet run --project Framework.Admin -- --hash "비밀번호"` |
| `Admin:ApiKey` | - | Framework.Api `.env` `ADMIN_API_KEY`와 동일 값 |

**[보안 주의 — PasswordHash 생성 절차]**
```
dotnet run --project Framework.Admin -- --hash "임시비밀번호"
# 출력된 해시를 복사하여 .env 또는 appsettings 갱신
# ※ 평문 비밀번호가 셸 히스토리/프로세스 목록에 기록됨
# 해시 생성 후 즉시: history -c (bash) 또는 Clear-History (PowerShell)
# 또는 격리된 1회용 환경에서 실행 권장
```

---

## 4. [필수] Google Play 연동 준비 사항

라이브 배포 전 Google Cloud / Play Console 설정.

| # | 단계 | 위치 | 비고 |
|---|---|---|---|
| 1 | 서비스 계정 생성 | Google Cloud Console > IAM > 서비스 계정 | JSON 키 발급 |
| 2 | Play Console 권한 부여 | Play Console > 설정 > API 액세스 | "주문 관리" 권한 |
| 3 | Pub/Sub 설정 | Google Cloud Pub/Sub | Topic 생성 + Push subscription 대상 = `/api/iap/google/rtdn` |
| 4 | License Testers 등록 | Play Console > 설정 > License Testers | 테스트 환경 |

---

## 5. 마이그레이션 운영

### 5.1 표준 절차
```
dotnet ef migrations add <MigrationName> --project Framework/Framework.Infrastructure --startup-project Framework/Framework.Api
dotnet ef database update --project Framework/Framework.Infrastructure --startup-project Framework/Framework.Api
```

### 5.2 xmin 동시성 토큰 마이그레이션 — **수동 절차 필수**

`PlayerItem.Quantity` / `Mail.IsClaimed` / `IapPurchase.Status` 등 xmin 토큰을 새 엔티티에 추가할 때:

1. `AppDbContext`에 매핑 추가:
   ```csharp
   .Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid")
   .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
   ```
2. `dotnet ef migrations add <Name>` 실행
3. **생성된 마이그레이션 `Up()`/`Down()` 본문을 수동으로 비울 것** + 한국어 사유 주석
   - EF Core가 `AddColumn<uint>("xmin", ...)`을 자동 생성하지만, PostgreSQL은 시스템 컬럼 충돌로 거부
   - 모델 스냅샷(`Designer.cs` / `AppDbContextModelSnapshot.cs`)은 그대로 유지
4. `dotnet ef database update` — 빈 본문이라 DDL 0건, 마이그레이션 적용 기록만 남음

> 일반 마이그레이션 파일 수동 수정은 안티패턴이지만 xmin은 EF Core/Npgsql 공식 가이드의 알려진 예외. 다른 엔티티에 동시성 토큰 추가 시에도 위 절차 그대로.

### 5.3 운영 환경 적용 체크리스트
- [ ] 백업 확인 (pg_dump 또는 볼륨 스냅샷)
- [ ] 점검 모드 ON 권장 (다운타임 동반 마이그레이션 시)
- [ ] `dotnet ef migrations script` 로 SQL 사전 검토 가능
- [ ] 적용 후 `/health` 200 확인

---

## 6. [성능] DB 인덱스 미적용 항목

현재 적용된 인덱스는 **데이터 무결성(유니크 제약) 목적의 필수 인덱스만 존재**. 성능용 세컨더리 인덱스는 의도적으로 추가하지 않았다 — 유저 수가 늘어날 때 적용 전/후 성능 비교 후 추가.

**인덱스 추가 위치**: `Framework.Infrastructure/Persistence/AppDbContext.cs` `OnModelCreating()`

| 테이블 | 컬럼 | 사용 쿼리 | 예상 효과 |
|---|---|---|---|
| `Mails` | `PlayerId` + `IsClaimed` | 미수령 우편 조회 | 우편함 조회 속도 개선 |
| `Mails` | `ExpiresAt` | 만료 우편 정리 | 만료 처리 속도 개선 |

---

## 7. SystemConfig 운영 키

`SystemConfig` 테이블에 저장되는 키 전체. 키 정의: `Framework.Domain/Constants/SystemConfigKeys.cs`. **Admin > 시스템 설정 페이지에서 GUI로 관리 가능** — 직접 DB 수정 비권장.

### 7.1 점검 모드
| 키 | 기본값 | 설명 |
|---|---|---|
| `maintenance_mode` | `"false"` | 점검 수동 강제 ON/OFF (`"true"`/`"false"`) |
| `maintenance_start_at` | `""` | 점검 예약 시작 시각 (ISO 8601 UTC, 빈값이면 미예약) |
| `maintenance_end_at` | `""` | 점검 예약 종료 시각 (ISO 8601 UTC, 빈값이면 미예약) |

### 7.2 앱 버전
| 키 | 기본값 | 설명 |
|---|---|---|
| `client_app_min_version` | `""` | 강제 업데이트 기준 최소 버전 — 이 버전 미만 클라이언트는 업데이트 강제. 서버 버전과 무관, 앱스토어 배포 Unity 빌드 기준 |
| `client_app_latest_version` | `""` | 현재 최신 앱 버전 — 소프트 업데이트 안내용 |

### 7.3 일일 보상
| 키 | 기본값 | 설명 |
|---|---|---|
| `daily_reward_active_month` | `"202604"` | 현재 활성 연월 (YYYYMM) — 월 전환 감지용, 자동 갱신 |
| `daily_reward_day_boundary_hour_kst` | `"0"` | 하루 기준 시각 KST 시(0~23) |
| `daily_reward_day_boundary_minute_kst` | `"0"` | 하루 기준 시각 KST 분(0~59) |
| `daily_reward_default_item_id` | `""` (미설정) | 월 28회 초과 시 지급할 기본 보상 아이템 ID. 빈값이면 보상 미발송 |
| `daily_reward_default_item_count` | `"0"` | 기본 보상 아이템 수량. 0이면 보상 미발송 |

기준 시각(00:00 기본) 미만이면 전날 날짜로 게임 날짜 계산. cycleDay는 이번 달 로그인 횟수 기반(1번째 로그인 = Day 1, 28번째 = Day 28, 29번째 이후 = 기본 보상).

---

## 8. 모니터링 & 헬스체크

| 항목 | 위치 | 비고 |
|---|---|---|
| `/health` | API | DB 연결 + 서비스 상태. 200 OK / 503 Unhealthy |
| Admin 헤더 헬스 인디케이터 | Admin Blazor 헤더 | API `/health` 호출 결과 시각화 |
| Serilog 파일 로그 | `logs/` 디렉토리 (환경별) | RequestLogging + Enricher 4종 (FromLogContext / MachineName / Environment / Application) |
| AdminNotification 알림 | Admin > `/admin-notifications` 페이지 | RTDN 환불, 동시성 충돌 한도 초과 등 운영 이슈 |

### 8.1 점검할 메트릭 (수동 또는 외부 APM 연동 시)
- `/health` 503 발생률
- DB 트랜잭션 retry 빈도 (`EnableRetryOnFailure` 5회)
- IAP verify 동시성 충돌 (`IapVerifyConcurrencyExhausted` AdminNotification)
- Rate Limit 429 누적 (Admin > 보안 감시 페이지 또는 `RateLimitLog` 테이블)

---

## 9. PII 보관기간 정책 — 운영 측면

`PiiRetentionCleanupService`가 매일 KST 03:00 자동 실행. 보관기간 표·법적 근거·정책 결정 배경은 `DEVNOTES.md` `[설계 결정] PII 자동 보관기간 정책` 참조.

**운영 동작**
- 매일 KST 03:00 실행, 5000행 청크 단위 ExecuteDelete/Update
- 비상 정지: `appsettings.json`의 `PiiRetention.Enabled = false`
- 보관기간/실행 시각 변경: `appsettings.json` `PiiRetention` 섹션 (재배포 필요)

**모니터링 권고**
- HealthCheck 통합은 미적용 — 정지 시 보관기간 위반 위험. 별도 라운드 권고 (`DEVNOTES.md [미구현]` 참조)
- 마지막 성공 시각 추적 시스템 도입 검토 (예: `Serilog` + 외부 알림)

**다중 인스턴스 운영 시 주의**
- 현재 단일 인스턴스 가정. 다중 컨테이너 운영 시 PostgreSQL advisory lock(`pg_try_advisory_lock`) 도입 필요 — `DEVNOTES.md [미구현]` 참조

---

## 10. Test 프로젝트 가이드

### 10.1 위치
- `Framework/Framework.Tests/` — 단일 테스트 프로젝트
- `Infrastructure/` — 테스트 공용 헬퍼
  - `TestDbContextFactory.cs` — InMemory DbContext 직접 생성
  - `TestServiceProviderBuilder.cs` — InMemory DbContext + 로깅 기본 DI 세팅
  - `UnitOfWorkSubstitute.cs` — NSubstitute IUnitOfWork 패스스루 헬퍼 (ExecuteInTransactionAsync 람다 실행)
- `Unit/Smoke/` — DI 스모크 테스트 (서비스 Resolve 가능 여부)
- `Unit/Exp/` — ExpService 단위 테스트 (AddExpAsync 10케이스)
- `Unit/Auth/` — AuthService 단위 테스트 (GuestLogin/Refresh/Logout 16케이스)
- `Integration/` — PostgreSQL 의존 테스트 (Testcontainers 도입 후 사용 예정, 현재 빈 폴더)

### 10.2 작성 규칙
- xUnit v3 + NSubstitute + EF InMemory
- 한국어 주석 (CLAUDE.md 규칙 동일)
- DB 의존 단위 테스트는 `TestDbContextFactory.Create()` 사용
- DI 의존 테스트는 `TestServiceProviderBuilder.CreateBaseServices()` 후 `Add*Services()` 호출
- Repository 모킹: `Substitute.For<IXxxRepository>()`
- 명명 규칙: `메서드_조건_기대결과` 예) `Grant_DuplicateSourceKey_ReturnsAlreadyGranted`

### 10.3 xmin / 동시성 / Raw SQL 테스트
- InMemory에서는 **동작 불가**
- 도입 시: `Testcontainers.PostgreSql` 패키지 추가 + Docker Desktop 필수
- 별도 라운드에서 결정

### 10.4 실행
- 전체: `dotnet test Framework/Framework.Tests/Framework.Tests.csproj`
- Unit만: `--filter "FullyQualifiedName~Unit"`
- Coverage: `--collect:"XPlat Code Coverage"`

### 10.5 도입 배경
- H-4 (round_20260503) — 인프라/스모크 셋업 완료
- 2026-05-07 — 1단계 단위 테스트 구현: ExpService(10개) + AuthService GuestLogin/Refresh/Logout(16개). 총 31개 통과. GoogleLoginAsync 등 나머지 AuthService 메서드는 2단계로 보류

---

## 11. 배포 체크리스트 (라이브 출시 직전)

- [ ] `.env` 모든 시크릿 실제 값 채움 (§2/§3)
- [ ] `appsettings.Development.json` 더미값이 운영에 노출 안 되는지 확인 (.env가 우선)
- [ ] Google Play Console 설정 완료 (§4)
- [ ] DB 백업 정책 설정 (pg_dump 또는 볼륨 스냅샷, 최소 1일 1회 / 30일 보관)
- [ ] 마이그레이션 운영 DB 적용 완료 (§5)
- [ ] `/health` 200 확인
- [ ] Admin 로그인 동작 (PasswordHash 운영용으로 갱신)
- [ ] DEBUG 빌드 아님 확인 (Release 컴파일 + `#if DEBUG` 우회 코드 제외 확인)
- [ ] `Framework.Api/Program.cs`/`Framework.Admin/Program.cs`의 `#if DEBUG` 블록 — Release 자동 제외
- [ ] HTTPS 인증서 적용 (리버스 프록시 또는 직접)
- [ ] CSP 헤더 도입 검토 (현재 미적용 — `DEVNOTES.md [기술 부채]` 참조)

---

## 12. 운영 중 발생 가능한 이슈 대응

| 증상 | 원인 후보 | 대응 |
|---|---|---|
| 모든 API 503 | 점검 모드 ON 또는 DB 단절 | Admin > 시스템 설정에서 점검 OFF, `/health` 확인 |
| 결제 검증 실패 누적 | Google Play 서비스 계정 권한 만료 | Play Console에서 권한 재확인 (§4) |
| RTDN 알림 미수신 | Pub/Sub subscription endpoint 변경 또는 OIDC Audience 불일치 | `Iap:Google:RtdnAudience` 와 Pub/Sub 설정 일치 확인 |
| Rate Limit 429 폭증 | 어뷰징 또는 정상 사용 한도 부족 | Admin > 보안 감시에서 IP/PlayerId 분석, 한도 조정 |
| AdminNotification "IapVerifyConcurrencyExhausted" | verify ↔ RTDN 동시성 한도 초과 | 발생 빈도 모니터링, 잦으면 별 라운드(M-29 후속) |
| 우편 수령 실패 다발 | xmin 동시성 충돌 한도 초과 | `RewardDispatcher`/`MailService` 로그 확인, 재시도 횟수 조정 검토 |

---

## 13. 추가 참고

- 설계 결정/박제: `DEVNOTES.md`
- 클라이언트 구현: `CLIENT_GUIDE.md`
- 코드 컨벤션: `CLAUDE.md`
- 미구현 항목/기술 부채: `DEVNOTES.md` `[미구현]` / `[기술 부채]` 섹션
- 라운드 보고서: `REVIEW_REPORT.md`

---

# 변경 이력

| 일자 | 변경 |
|---|---|
| 2026-05-05 | 최초 작성 — DEVNOTES에서 운영 절차 추출 + 배포 체크리스트 신규 |
