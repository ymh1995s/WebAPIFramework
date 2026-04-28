# Dev Notes

## Feature Status

| 기능 | 설명 |
|---|---|
| JWT 인증 | 게스트 로그인(DeviceId), AccessToken/RefreshToken 발급, 로그아웃 |
| 구글 OAuth 연동 | Google IdToken 검증, 신규 로그인 및 기존 계정 연결, 계정 충돌 감지(409)/해소, 게스트 계정 소프트 딜리트 |
| 게스트 결제 차단 | `[RequireLinkedAccount]` 필터 — 구글 미연동 계정의 결제 엔드포인트 접근 시 403 반환 (결제 컨트롤러 구현 시 부착 필요) |
| 랭킹 시스템 | 게임 결과 점수 기록, 상위 N명 랭킹 조회 |
| 인벤토리 관리 | 플레이어 보유 아이템 조회, 아이템 획득 |
| 우편 시스템 | 우편 수신/수령 API, Admin 단건·일괄 발송 |
| 일일 로그인 보상 | 로그인 시 당일 보상 우편 발송 (이번 달 로그인 횟수 기반, 매월 리셋). 빈 일자는 보상 없음. Current/Next 2슬롯 방식으로 이번 달·다음 달 보상 예약 관리. KST 하루 기준 시각(기본 00:00) Admin 설정 가능 |
| 매치메이킹 | SignalR 기반 실시간 매칭, 대기열 관리 |
| 아이템 마스터 관리 | Admin CRUD (추가/수정/소프트삭제), 보유 유저 수 확인 |
| Admin 인증 | X-Admin-Key 헤더 기반 API 접근 제어, 미들웨어에서 Admin Key 인증 시 모든 [Authorize] 엔드포인트 접근 허용 |
| 시스템 설정 | 점검 모드, 앱 버전, 일일 보상 기준 시각 등 SystemConfig Admin 제어 |
| 어뷰징 방어 | Rate Limiting (IP 기준), 429 발생 시 DB 로그, Admin 보안 감시 페이지 |
| 점검 모드 | 수동 ON/OFF 및 시각 예약, 미들웨어에서 503 차단, Admin은 점검 중에도 접근 가능 |
| 계정 탈퇴 | DELETE /auth/withdraw, 플레이어 즉시 하드 삭제, CASCADE로 모든 연관 데이터 삭제 (개인정보보호법 제21조 준수) |
| 클라이언트 앱 버전 체크 | GET /api/version/check, 강제 업데이트 여부 반환, Admin에서 최소/최신 버전 설정 (서버 버전 아님 — 앱스토어 배포 Unity 빌드 기준) |
| 공지 시스템 | GET /api/notices/latest, 최신 활성 공지 1개 반환. 클라이언트가 NoticeId를 PlayerPrefs에 저장해 1회성 표시. Admin CRUD 관리 페이지 포함 |
| 플레이어 문의 | POST /api/inquiries 제출, GET /api/inquiries 내 목록 조회. Admin 답변 등록. 소원수리함 형태(자유 텍스트). Blazor 테스트 페이지 포함 |
| 감사 로그 | 재화/아이템 변동 추적. Item.AuditLevel(AnomalyOnly/Full) + AnomalyThreshold 기준으로 저장 범위 차별화. Admin `/audit-logs` 페이지에서 플레이어·아이템·기간·이상치 필터 조회. 현재 훅은 `MailService.ClaimAsync` 적용 |

---

## [필수] Framework.Api/appsettings.json 교체값
라이브 배포 전 .env 파일에 반드시 실제 값을 채워야 하는 항목 목록입니다.

| 키 | .env 변수명 | 교체 방법 |
|---|---|---|
| `Jwt:SecretKey` | `JWT_SECRET_KEY` | 32자 이상 랜덤 문자열로 교체 |
| `Admin:ApiKey` | `ADMIN_API_KEY` | 랜덤 문자열로 교체 |
| `Google:ClientId` | `GOOGLE_CLIENT_ID` | Google Cloud Console에서 OAuth 클라이언트 ID 발급 |
| `ConnectionStrings:Default` | `POSTGRES_PASSWORD` | 운영 DB 비밀번호 설정 |

## [필수] Framework.Admin/appsettings.json 교체값
- `ApiBaseUrl` — 현재 `https://api.overture.io.kr`. 도메인 변경 시 교체 필요
- `Admin:Password` — 운영툴(Blazor) 로그인 비밀번호. 운영 전 강력한 비밀번호로 직접 입력 필요
- `Admin:ApiKey` — Framework.Api의 `.env` `ADMIN_API_KEY`와 동일한 값으로 설정 필요

## [성능] DB 인덱스 미적용 항목
현재 적용된 인덱스는 데이터 무결성(유니크 제약) 목적의 필수 인덱스만 존재합니다.
성능용 세컨더리 인덱스는 의도적으로 추가하지 않았습니다 — 유저 수가 늘어날 때 적용 전/후 성능 비교 후 추가 권장합니다.
인덱스 추가 위치: `Framework.Infrastructure/Persistence/AppDbContext.cs` `OnModelCreating()`

| 테이블 | 컬럼 | 사용 쿼리 | 예상 효과 |
|---|---|---|---|
| `PlayerRecords` | `Score` | 랭킹 정렬 (`ORDER BY Score DESC`) | 랭킹 조회 속도 개선 |
| `Mails` | `PlayerId` + `IsClaimed` | 미수령 우편 조회 | 우편함 조회 속도 개선 |
| `Mails` | `ExpiresAt` | 만료 우편 정리 | 만료 처리 속도 개선 |


## [SystemConfig] 일일 보상 관련 키
| 키 | 기본값 | 설명 |
|---|---|---|
| `daily_reward_active_month` | `"202604"` | 현재 활성 연월 (YYYYMM) — 월 전환 감지용, 자동 갱신 |
| `daily_reward_day_boundary_hour_kst` | `"0"` | 하루 기준 시각 KST 시(0~23) |
| `daily_reward_day_boundary_minute_kst` | `"0"` | 하루 기준 시각 KST 분(0~59) |
| `daily_reward_default_item_id` | `""` (미설정) | 월 28회 초과 시 지급할 기본 보상 아이템 ID. 빈값이면 보상 미발송 |
| `daily_reward_default_item_count` | `"0"` | 기본 보상 아이템 수량. 0이면 보상 미발송 |

기준 시각(00:00 기본) 미만이면 전날 날짜로 게임 날짜 계산. Admin 시스템 설정 페이지에서 변경 가능.
cycleDay는 이번 달 로그인 횟수 기반 (1번째 로그인 = Day 1, 28번째 = Day 28, 29번째 이후 = 기본 보상).

## [기술 부채] 검토 항목
- **Admin 컨트롤러 익명 객체 응답** — 일부 Admin 컨트롤러가 DTO 없이 익명 객체로 응답을 구성함. Admin 전용 단순 조회라 즉각 위험은 낮으나, 신규 Admin 기능 구현 시에는 DTO 정의 원칙 준수 필요

## [미구현] 추가 개발 필요 항목
- **공지사항 페이지** [선택] — 현재는 1회성 텍스트 공지만 구현됨. 공지 이력 열람, 카테고리 분류 등 게시판 형태가 필요해지면 별도 페이지 추가 고려
- **감사 로그 훅 확장** — 현재는 `MailService.ClaimAsync`에만 훅 적용됨. 상점 구매/스테이지 보상/Admin 직접 지급 등 기능 구현 시 `IAuditLogService.RecordAsync` 호출 추가 필요
- **백업 정책** — DB 백업은 애플리케이션 관할 아님. Docker로 운영 중인 PostgreSQL 컨테이너/볼륨 레벨에서 별도 설정 필요 (pg_dump, 볼륨 스냅샷 등). 최소 1일 1회 백업, 30일 보관 권장
- **광고 보상 서버사이드 검증(SSV)** — 광고 시청 보상 지급 시 클라이언트 조작 방지를 위해 구글/애플 서버 검증 필요
- **인앱 결제 영수증 검증** — Google Play / Apple IAP 결제 후 서버에서 영수증 진위 검증 필요
- **이벤트 기간 관리** [중요도 낮음] — 기간 한정 이벤트 시작/종료 관리. 클라이언트가 현재 이벤트 진행 여부를 서버에 질의. 게임마다 구조가 달라 범용 설계 필요
- **로그/APM 도구 연동** [중요도 낮음] — 현재 파일 로그(Serilog) 기반. 유저 증가 시 ELK Stack + Elastic APM 연동 권장 (APM이 ELK 위에서 동작하므로 세트로 도입). 가벼운 대안으로 Seq(컨테이너 1개, .NET 친화적) 또는 Grafana+Loki 가능. Serilog 싱크 추가 + Program.cs 한 줄로 연동 가능
