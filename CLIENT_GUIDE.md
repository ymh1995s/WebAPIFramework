# CLIENT_GUIDE — Unity 클라이언트 구현 가이드

이 문서는 Web API Framework 백엔드를 호출하는 **Unity 클라이언트 구현 walkthrough**다. 앱 부팅부터 종료까지 순차로 따라가며 모든 클라이언트 측 기능을 구현할 수 있도록 구성되어 있다.

- **청중**: Unity 클라이언트 개발자 (사람 또는 Claude Code 에이전트)
- **선행 문서**: 서버 측 절차는 `SERVER_GUIDE.md` 참조. 설계 의도/박제는 `DEVNOTES.md` 참조
- **각 단계**: 선행 조건 → UI → 요청 → 응답 → 에러 처리 → 인수 기준 → 서버 코드 참조 형식
- **구현 순서**: 1번부터 차례대로. 각 단계는 self-contained

---

## 사전 준비

| 항목 | 값/권장 |
|---|---|
| API Base URL | 환경별 (예: `https://api.yourdomain.com`) — 서버 운영자에게 확인 |
| HTTP 클라이언트 | `UnityWebRequest` 또는 동등 라이브러리 |
| 응답 타임아웃 | **60초 이상** (서버 DB transient retry 최대 50초 가능 — `8부 27` 참조) |
| JSON 직렬화 | `Newtonsoft.Json` 권장 (record/nullable 매핑 안정) |
| 외부 SDK | Google Sign-In (IdToken), Google Play Billing (IAP), Unity Ads/IronSource (광고) |

## 공통 헤더

| 헤더 | 값 | 적용 |
|---|---|---|
| `Authorization` | `Bearer {AccessToken}` | JWT 필요 엔드포인트 (대부분의 `/api/*`) |
| `Content-Type` | `application/json` | POST/DELETE 본문 있는 요청 |

## 인증/Rate Limit 정책 요약

- **인증 정책 (`auth`)**: 미인증 IP 분당 15회 / 인증 PlayerId 분당 30회
- **인게임 정책 (`game`)**: PlayerId 분당 120회 (미인증 시 IP)
- **IAP 검증 정책 (`iap-verify`)**: PlayerId 분당 20회
- 한도 초과 시 **HTTP 429** + `Retry-After` 헤더

---

# 1부 — 앱 부팅

## 1. 점검 모드 체크 (전역 미들웨어)

**선행 조건**: 없음
**UI 필요**: 점검 안내 화면 (메시지 + 재시도 버튼)
**호출 시점**: 별도 호출 없음. **모든 API 응답을 인터셉터로 감지**

서버는 점검 모드 ON 상태에서 모든 요청에 503을 반환한다. 클라이언트는 응답 인터셉터에서 503 + 응답 본문 `{"message":"서버 점검 중..."}` 패턴을 감지해 점검 화면으로 전환한다.

**응답 (점검 중)**
```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json

{"message":"서버 점검 중입니다. 잠시 후 다시 시도해주세요."}
```

**처리**
- 503 수신 → 사용자 액션 차단 + 점검 화면 표시
- 일정 간격(예: 30초)으로 재시도 (예: `GET /api/version/check` 호출하여 200 회복 확인)

**구현 인수 기준**
- [ ] 어떤 API 호출이든 503 수신 시 점검 화면으로 전환
- [ ] 재시도 시 자동 복귀

**서버 코드 참조**: `Framework/Framework.Api/Program.cs:170-186`

---

## 2. 강제 업데이트 체크

**선행 조건**: 점검 통과
**UI 필요**: 강제 업데이트 다이얼로그 (스토어 이동 버튼)
**호출 시점**: 점검 통과 후 즉시

**요청**
```http
GET /api/version/check?version={Application.version}
```
- 인증 불필요
- `version`: Unity 빌드 버전 (예: `1.0.0`)

**응답 (성공)**
```json
{
  "isForceUpdate": false,
  "latestVersion": "1.2.0"
}
```

**에러 처리**
| Status | 처리 |
|---|---|
| 400 | 버전 형식 오류 — 클라이언트 빌드 버그. 로그 후 진행 |
| 503 | 점검 중 (1번 처리) |
| 429 | Rate Limit — 잠시 후 재시도 |

**처리**
- `isForceUpdate=true` → 스토어 이동 다이얼로그 표시 (게임 진입 차단)
- `isForceUpdate=false` → 다음 단계로

**구현 인수 기준**
- [ ] 강제 업데이트 시 스토어 이동만 가능
- [ ] 일반 진행 시 다음 단계로

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/VersionController.cs:26`

---

## 3. 최신 공지 표시

**선행 조건**: 버전 체크 통과
**UI 필요**: 공지 팝업 (제목/본문/닫기)
**호출 시점**: 버전 체크 통과 후 즉시 (로그인 전)

**요청**
```http
GET /api/notices/latest
```
- 인증 불필요

**응답 (성공)**
```json
{ "id": 12, "content": "신규 이벤트 안내..." }
```

**응답 (활성 공지 없음)**
```http
HTTP/1.1 204 No Content
```

**처리 — 1회성 보장**
- `PlayerPrefs`에서 마지막으로 본 `lastSeenNoticeId` 조회
- 응답 `id !== lastSeenNoticeId` 일 때만 팝업 표시
- 사용자가 닫으면 `lastSeenNoticeId = id` 저장

**구현 인수 기준**
- [ ] 동일 공지는 한 번만 표시
- [ ] 새 공지 발행 시 자동 표시

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/NoticesController.cs:22`

---

# 2부 — 인증

## 4. 게스트 로그인

**선행 조건**: 1~3 부팅 완료
**UI 필요**: "게스트로 시작" 버튼
**호출 시점**: 사용자가 게스트 버튼 클릭 (또는 자동 로그인 시 RefreshToken 부재)

**DeviceId 발급 규칙**
- 영문/숫자/하이픈/언더스코어, 8~64자
- `SystemInfo.deviceUniqueIdentifier` 또는 자체 생성 GUID 권장
- **PlayerPrefs에 영구 저장** — 재설치/계정 복구의 근거

**요청**
```http
POST /api/auth/guest
Content-Type: application/json

{ "deviceId": "abc123def456..." }
```

**응답 (성공)**
```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "base64token...",
  "playerId": "uuid-form",
  "isNewPlayer": true
}
```

**에러 처리**
| Status | 본문/사유 | UI 처리 |
|---|---|---|
| 400 | DeviceId 누락/형식 오류 | 재발급 후 재시도 |
| 403 | 밴된 계정 | 안내 다이얼로그 + 종료 |
| 429 | Auth Rate Limit | 백오프 후 재시도 (분당 15회) |

**토큰 저장**
- `accessToken` → 메모리 (앱 종료 시 사라짐)
- `refreshToken` → 보안 저장소 (Android Keystore / iOS Keychain) — `부록 C` 참조

**구현 인수 기준**
- [ ] DeviceId 영구 저장
- [ ] 토큰 저장 후 메인 로비 진입
- [ ] 밴 응답 시 종료

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:27`

---

## 5. 구글 로그인

**선행 조건**: 1~3 부팅 완료. Google Sign-In SDK 통합
**UI 필요**: "구글로 시작" 버튼
**호출 시점**: 사용자가 구글 버튼 클릭

**IdToken 발급**
- Google Sign-In SDK에서 발급 (서버 측 `Google:ClientId`와 일치하는 OAuth 클라이언트 사용)
- 서버는 IdToken 자체를 검증 (Audience/만료/서명) — 클라가 토큰 디코딩 불필요

**요청**
```http
POST /api/auth/google
Content-Type: application/json
[Authorization: Bearer {AccessToken}]   # 게스트 로그인 상태에서 호출 시 충돌 감지에 활용 (선택)

{ "idToken": "eyJhbGci..." }
```

**응답 (성공 — 신규 또는 기존 매칭)**
```json
{
  "accessToken": "...", "refreshToken": "...",
  "playerId": "uuid", "isNewPlayer": false
}
```

**응답 (계정 충돌 — 409)**
이미 다른 계정이 해당 GoogleId를 보유 중일 때:
```json
{
  "errorCode": "GOOGLE_ACCOUNT_CONFLICT",
  "existingPlayer": { "playerId": "...", "nickname": "기존유저", "level": 5, "createdAt": "...", "lastLoginAt": "..." },
  "currentGuestPlayer": { "playerId": "...", "nickname": "...", "level": 1, ... }
}
```

**에러 처리 매트릭스**
| Status | ErrorCode | UI 처리 |
|---|---|---|
| 200 | - | 토큰 저장 + 메인 진입 |
| 401 | - | "유효하지 않은 IdToken" 안내, 재로그인 유도 |
| 409 | GOOGLE_ACCOUNT_CONFLICT | **충돌 해소 다이얼로그** ("기존 계정으로 전환?" / "취소") |
| 429 | - | 백오프 후 재시도 |

**충돌 해소 흐름**
- 사용자가 "기존 계정으로 전환" 선택 → `7. 구글 계정 충돌 해소` 호출
- 사용자가 "취소" 선택 → 게스트 상태 유지

**구현 인수 기준**
- [ ] IdToken 발급/송신 동작
- [ ] 409 응답 시 충돌 다이얼로그 표시
- [ ] 정상 시 토큰 저장 후 진입

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:82`

---

## 6. 게스트 → 구글 연동

**선행 조건**: 게스트 로그인 상태 (JWT 보유)
**UI 필요**: "구글 계정 연동" 버튼 (설정 화면)
**호출 시점**: 게스트 사용자가 자발적으로 구글 연동 (결제 차단 해제 등)

**요청**
```http
POST /api/auth/link/google
Authorization: Bearer {AccessToken}
Content-Type: application/json

{ "idToken": "eyJ..." }
```

**응답 (성공)** — 200 OK (본문 없음). 기존 게스트 계정에 GoogleId만 추가됨.

**에러 처리 매트릭스**
| Status | ErrorCode | UI 처리 |
|---|---|---|
| 200 | - | "연동 완료" 토스트 |
| 401 | - | IdToken 무효 — 재시도 |
| 409 | GOOGLE_ACCOUNT_CONFLICT | 다른 계정에 이미 연동됨 — 다이얼로그(전환/취소) |
| 409 | - (메시지) | 이미 다른 GoogleId 연동됨 (이중 연동 시도) |

**구현 인수 기준**
- [ ] 정상 연동 시 게스트 데이터 보존 + GoogleId 추가
- [ ] 409 충돌 시 충돌 해소 흐름 유도

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:111`

---

## 7. 구글 계정 충돌 해소

**선행 조건**: 5번 또는 6번에서 409 GOOGLE_ACCOUNT_CONFLICT 수신, 사용자가 "기존 계정으로 전환" 선택
**UI 필요**: 전환 확인 다이얼로그 ("게스트 진행도가 사라집니다 — 계속하시겠습니까?")
**호출 시점**: 충돌 다이얼로그에서 "전환" 클릭

**요청**
```http
POST /api/auth/google/resolve-conflict
Authorization: Bearer {게스트AccessToken}
Content-Type: application/json

{ "idToken": "eyJ..." }
```

**응답 (성공)** — `TokenResponseDto` (4번과 동일 형식). 게스트 계정은 SoftDelete됨, 기존 계정으로 토큰 발급.

**에러 처리**
| Status | UI 처리 |
|---|---|
| 200 | 새 토큰으로 교체 + 메인 진입 |
| 400 | 충돌 상황이 아닌 호출 — 로직 오류, 재로그인 |
| 401 | IdToken 무효 |

**중요 의무 사항**
- 사용자에게 **게스트 진행도가 영구 손실됨**을 명시적으로 고지 (UX 의무)
- 전환 후 토큰을 즉시 새 토큰으로 교체

**구현 인수 기준**
- [ ] 안내 다이얼로그 명시적 경고
- [ ] 응답 후 메모리/저장소 토큰 모두 교체
- [ ] 게스트 진행도가 사라진 메인 로비 진입

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:139`

---

## 8. AccessToken 자동 회전 (401 인터셉터)

**선행 조건**: 게스트/구글 로그인 후 RefreshToken 보유
**UI 필요**: 없음 (내부 동작)
**호출 시점**: 모든 인증 필요 API 호출의 응답이 401일 때 자동 트리거

**흐름**
```
[원 요청 401] → POST /api/auth/refresh → 새 토큰 받음 → 원 요청 재시도(1회)
```

**요청**
```http
POST /api/auth/refresh
Content-Type: application/json

{ "refreshToken": "base64token..." }
```

**응답 (성공)** — `TokenResponseDto`. **새 RefreshToken**도 함께 발급되므로 저장소 갱신 필수.

**에러 처리**
| Status | UI 처리 |
|---|---|
| 200 | 토큰 갱신 후 원 요청 재시도 |
| 401 | RefreshToken도 만료/무효 — **로그인 화면으로 강제 이동** |

**구현 주의 사항**
- 401 인터셉터가 무한 루프에 빠지지 않도록 가드 (refresh 자체가 401이면 즉시 로그인 화면)
- 동시 다발 401 발생 시 **refresh 한 번만 수행** (mutex/queue로 직렬화)
- 새 RefreshToken을 보안 저장소에 즉시 갱신 (이전 토큰은 서버 DB에서 무효화됨)

**구현 인수 기준**
- [ ] AccessToken 만료 시 자동 회전
- [ ] RefreshToken 만료 시 로그인 화면 이동
- [ ] 동시 다발 401 단일 refresh로 처리

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:51`

---

## 9. 로그아웃

**선행 조건**: 로그인 상태
**UI 필요**: "로그아웃" 버튼 (설정 화면)

**요청**
```http
POST /api/auth/logout
Authorization: Bearer {AccessToken}
Content-Type: application/json

{ "refreshToken": "base64token..." }
```

**응답** — 204 No Content (멱등 — 이미 무효한 토큰도 204).

**처리**
- 메모리 AccessToken 삭제
- 보안 저장소 RefreshToken 삭제
- DeviceId는 **유지** (재로그인 시 동일 게스트 계정 복구)
- 로그인 화면으로 이동

**구현 인수 기준**
- [ ] 토큰 삭제 + 로그인 화면 이동
- [ ] DeviceId 유지

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:73`

---

## 10. 탈퇴 (법적 의무 안내 팝업)

**선행 조건**: 로그인 상태
**UI 필요**: **법적/마켓 정책 의무 다이얼로그** (`부록 A` 참조)
**호출 시점**: 사용자가 탈퇴 버튼 클릭 + 안내 확인

**의무 안내 항목** (다이얼로그 본문에 모두 포함 필수)
- 캐릭터/아이템/진행도 영구 삭제
- 미수령 우편/보상 소실
- 결제 보상 소실
- 재가입 시 데이터 복구 불가
- (개인정보보호법 §22) 파기 항목·범위 사전 고지

**요청**
```http
DELETE /api/auth/withdraw
Authorization: Bearer {AccessToken}
```

**응답** — 204 No Content (멱등 — 이미 탈퇴된 계정 재호출도 204).

**처리**
- 메모리/저장소 모든 토큰 삭제
- DeviceId 삭제 (재가입 시 신규 발급)
- 앱 종료 또는 로그인 화면

**구현 인수 기준**
- [ ] 안내 팝업 모든 항목 포함
- [ ] 사용자 확인 없이 호출 안 됨
- [ ] 탈퇴 후 토큰/DeviceId 삭제

**근거**:
- 개인정보보호법 §22
- Google Play User Data Policy (앱 내 계정 삭제 + 삭제 데이터 안내 의무)
- Apple App Store Review 5.1.1(v) (iOS 16+ 필수)

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/AuthController.cs:167`

---

# 3부 — 메인 로비 진입

## 11. 일일 로그인 보상 트리거

**선행 조건**: 로그인 완료
**UI 필요**: 없음 (자동) — 다만 보상 도착 토스트 권장
**호출 시점**: 메인 로비 진입 직후 1회

**요청**
```http
POST /api/dailylogin
Authorization: Bearer {AccessToken}
```

**응답 (성공)**
```json
{ "rewarded": true }
```
- `rewarded=true`: 오늘 첫 로그인 → 우편함에 보상 발송됨
- `rewarded=false`: 오늘 이미 보상 받음 (멱등)

**처리**
- `rewarded=true`인 경우 사용자에게 "일일 보상이 우편함에 도착했습니다" 토스트 권장
- 우편함 화면 진입 시 12~14번 흐름

**구현 인수 기준**
- [ ] 메인 진입 시 자동 호출
- [ ] 동일 날짜 재호출 시 멱등 동작
- [ ] 토스트 안내 (선택)

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/DailyLoginController.cs:26`

---

## 12. 1회 공지(HUD) 폴링

**선행 조건**: 로그인 완료
**UI 필요**: HUD 텍스트 영역 (배너/상단 띠)
**호출 시점**: 메인 진입 시 1회 (폴링 주기는 클라 자율 — 권장: 5~10분 간격 또는 화면 전환 시)

**요청**
```http
GET /api/shouts/active
Authorization: Bearer {AccessToken}
```

**응답 (성공)**
```json
[
  { "id": 5, "message": "긴급 점검 30분 후 진행됩니다", "createdAt": "...", "expiresAt": "..." },
  ...
]
```
- 빈 배열도 정상 (200 OK + `[]`)

**처리**
- 배열 순회하여 만료 안 된 메시지를 HUD에 표시
- 빈 배열이면 HUD 숨김
- 같은 메시지 중복 표시는 클라가 제어 (id 기반)

**구현 인수 기준**
- [ ] 활성 메시지 표시
- [ ] 빈 배열 시 HUD 숨김

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/ShoutsController.cs:26`

---

## 13. 우편함 조회

**선행 조건**: 로그인 완료
**UI 필요**: 우편함 화면 (목록 + 수령 버튼)
**호출 시점**: 사용자가 우편함 진입

**요청**
```http
GET /api/mails
Authorization: Bearer {AccessToken}
```

**응답 (성공)**
```json
[
  {
    "id": 100,
    "playerId": 42,
    "title": "일일 보상",
    "body": "오늘의 일일 보상입니다.",
    "itemId": null, "itemName": null, "itemCount": 0,
    "isRead": false, "isClaimed": false,
    "createdAt": "...", "expiresAt": "...",
    "exp": 0,
    "mailItems": [
      { "itemId": 1, "itemName": "Gold", "quantity": 100 },
      { "itemId": 2, "itemName": "Gems", "quantity": 5 }
    ]
  }
]
```

**핵심 사항**
- `mailItems` 배열을 우선 사용 (다중 아이템 표준 경로)
- `itemId`/`itemName`/`itemCount`는 deprecated 단일 아이템 호환 — null인 경우 다중으로 처리
- `mailItems` 안에 **통화도 ItemId=1(Gold)/2(Gems)** 로 포함됨
- `exp`는 우편 첨부 경험치 — 수령 시 자동 레벨업 처리

**구현 인수 기준**
- [ ] 우편 목록 표시
- [ ] mailItems + exp 모두 사용자에게 시각화
- [ ] isClaimed=true는 회색/비활성 표시

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/MailsController.cs:25`

---

## 14. 우편 수령

**선행 조건**: 13번에서 미수령 우편 ID 보유
**UI 필요**: 수령 버튼
**호출 시점**: 사용자가 수령 버튼 클릭

**요청**
```http
POST /api/mails/{id}/claim
Authorization: Bearer {AccessToken}
```

**응답**
| Status | 의미 |
|---|---|
| 200 | 수령 성공 — 인벤토리/Exp 자동 반영 |
| 400 | 이미 수령했거나 존재하지 않음 |

**처리**
- 200 → 우편 목록 갱신 + 인벤토리/레벨 갱신
- 400 → 새로고침 (서버 상태와 동기화)

**구현 인수 기준**
- [ ] 수령 성공 시 보상 알림
- [ ] 서버 동시성 토큰 충돌 시 자동 재시도 (서버 측 처리, 클라는 200/400만 처리)

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/MailsController.cs:34`

---

# 4부 — 게임 컨텐츠

## 15. 활성 스테이지 목록

**선행 조건**: 로그인 완료
**UI 필요**: 스테이지 선택 화면

**요청**
```http
GET /api/stages
Authorization: Bearer {AccessToken}
```

**응답 (성공)**
```json
[
  {
    "id": 1, "code": "stage-1-1", "name": "1-1 시작",
    "rewardTableCode": "stage:first:1-1",
    "rePlayRewardTableCode": "stage:replay:1-1",
    "rePlayRewardDecayPercent": 50,
    "expReward": 100,
    "requiredPrevStageId": null,
    "isActive": true, "sortOrder": 1
  }
]
```

**구현 인수 기준**
- [ ] sortOrder 순으로 표시
- [ ] isActive=false는 숨김

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Content/Player/StagesController.cs:36`

---

## 16. 내 스테이지 진행 현황

**선행 조건**: 15번 완료 (또는 병렬 호출)
**UI 필요**: 스테이지별 잠금/별/최고점수 표시

**요청**
```http
GET /api/stages/progress
Authorization: Bearer {AccessToken}
```

**응답 (성공)**
```json
[
  {
    "stageId": 1, "code": "stage-1-1", "name": "1-1 시작",
    "isCleared": true, "clearCount": 3,
    "bestScore": 9500, "bestStars": 3, "bestClearTimeMs": 45000,
    "isLocked": false, "sortOrder": 1
  }
]
```

**처리**
- `isLocked=true` → 잠금 아이콘 표시 (선행 스테이지 미클리어)
- 클리어 시 별 수/최고점수 표시

**구현 인수 기준**
- [ ] 잠금/해제 정확히 표시
- [ ] 별·점수·시간 시각화

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Content/Player/StagesController.cs:44`

---

## 17. 스테이지 클리어

**선행 조건**: 15/16번 완료, 사용자가 스테이지 플레이 후 결과 확정
**UI 필요**: 클리어 결과 화면 (Exp/보상 표시)
**호출 시점**: 게임 결과 확정 직후

**요청**
```http
POST /api/stages/{stageId}/complete
Authorization: Bearer {AccessToken}
Content-Type: application/json

{ "score": 9500, "stars": 3, "clearTimeMs": 45000 }
```
- `score` ≥ 0
- `stars` 0~3
- `clearTimeMs` ≥ 0

**응답 (성공)**
```json
{
  "isFirstClear": true,
  "clearCount": 1,
  "expGranted": 100,
  "firstRewardMessage": "최초 클리어 보상이 우편함에 발송되었습니다",
  "replayRewardMessage": null
}
```

**에러 처리 매트릭스**
| Status | 사유 | UI 처리 |
|---|---|---|
| 404 | 스테이지 없음/비활성 | 새로고침 (서버 마스터 변경) |
| 409 | 선행 스테이지 미클리어 | 잠금 안내, 진행 현황 새로고침 |
| 401 | 토큰 무효 | 8번 인터셉터 |

**처리**
- `firstRewardMessage`/`replayRewardMessage` 비어있지 않으면 우편함 알림
- `expGranted`로 캐릭터 레벨/Exp 갱신 (서버는 이미 처리됨, 클라는 표시만)

**구현 인수 기준**
- [ ] 점수/별/시간 검증 후 송신
- [ ] 보상 메시지 표시
- [ ] 잠금 응답 시 사용자 안내

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Content/Player/StagesController.cs:60`

---

## 18. 내 랭킹 조회

**선행 조건**: 로그인 완료, 게임 결과 1건 이상 (BestScore 보유)
**UI 필요**: 랭킹 화면 (내 순위 / 닉네임 / 점수)

**요청**
```http
GET /api/ranking/me
Authorization: Bearer {AccessToken}
```

**응답 (성공)**
```json
{ "rank": 42, "playerId": "uuid", "nickname": "홍길동", "bestScore": 9500 }
```

**처리**
- `rank` 표시 (1=최상위)
- 점수 0인 신규 플레이어는 서비스 정의에 따라 표시/숨김

**구현 인수 기준**
- [ ] 순위 표시
- [ ] 신규 플레이어 표시 정책 결정

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/RankingController.cs:24`

> **참고**: 게임 결과 기록 API는 현재 백엔드 미구현 (`부록 B`). 랭킹의 `bestScore`는 향후 매치메이킹/게임 결과 라운드에서 도입 예정.

---

# 5부 — 결제

## 19. IAP 영수증 검증

**선행 조건**:
- 로그인 완료 + **구글 계정 연동 완료** (게스트는 결제 차단됨, 20번 처리)
- Google Play Billing SDK 통합

**UI 필요**: 결제 진행 다이얼로그 + 결과 안내
**호출 시점**: Google Play 결제 완료 후 클라이언트가 `purchaseToken` 수신 시 즉시

**요청**
```http
POST /api/iap/google/verify
Authorization: Bearer {AccessToken}
Content-Type: application/json

{
  "productId": "com.yourgame.gem_pack_100",
  "purchaseToken": "ohgkb...",
  "orderId": "GPA.1234-5678-9012-34567"
}
```
- `productId`: 1~128자
- `purchaseToken`: 1~512자
- `orderId`: 선택 (Google: GPA.xxxx)

**응답 (성공)**
```json
{ "ok": true, "alreadyGranted": false, "purchaseId": 123 }
```
- `alreadyGranted=true`: 이미 처리된 토큰 (멱등 — 재시도 안전)
- `ok=true, alreadyGranted=false`: 첫 처리 — 보상 지급 완료

**에러 처리 매트릭스 (ProblemDetails 응답)**
| Status | ErrorCode | UI 처리 |
|---|---|---|
| 200 | - | 보상 지급 완료 안내 (우편 또는 직접 지급) |
| 400 | VALIDATION_FAILED | 입력 검증 — 클라 버그 |
| 401 | - | 토큰 만료 (8번) |
| 403 | - | RequireLinkedAccount — **20번 처리** |
| 404 | IAP_PRODUCT_NOT_FOUND | 상품 마스터 없음 — 운영자 문의 |
| 409 | IAP_RECEIPT_INVALID | 영수증 위변조/이상 — 재시도 X, 운영자 문의 |
| 422 | IAP_TOKEN_OWNERSHIP_MISMATCH | 다른 플레이어 토큰 사용 — 보안 사고 |
| 502 | IAP_VERIFIER_ERROR | Google API 일시 장애 — 잠시 후 재시도 |
| 503 | IAP_VERIFY_CONCURRENCY_EXHAUSTED | 동시성 충돌 — **잠시 후 재시도** (재시도 시 alreadyGranted 응답 가능) |

**처리 — 재시도 정책**
- 502/503/네트워크 오류: 지수 백오프 재시도 (최대 5회, 30초~5분 간격)
- **재시도 시 동일 `purchaseToken` 송신 — 서버는 멱등**
- 4xx (404/409/422/403): 재시도 X, 사용자 안내

**구현 인수 기준**
- [ ] purchaseToken 수령 즉시 송신
- [ ] 502/503 재시도 로직
- [ ] 403 시 20번 흐름으로 분기
- [ ] 성공 시 우편함 갱신 (보상이 우편으로 도착)

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/IapPurchaseController.cs:36`

---

## 20. 게스트 결제 차단 처리 (403 RequireLinkedAccount)

**선행 조건**: 19번 호출이 403 응답
**UI 필요**: "구글 계정 연동 필요" 안내 다이얼로그 (연동 버튼 + 취소)

**응답 (게스트 시도)**
```http
HTTP/1.1 403 Forbidden
Content-Type: application/json

{ "message": "구글 계정 연동이 필요합니다." }
```
※ 메시지 형식은 ProblemDetails 또는 단순 객체일 수 있음 — 403 코드만으로 분기

**처리**
- 결제 시도 차단 + 다이얼로그 표시
- "연동" 클릭 → 6번 흐름 (게스트→구글 연동) 호출
- 연동 성공 후 19번 재시도

**구현 인수 기준**
- [ ] 403 수신 시 결제 진행 차단
- [ ] 안내 다이얼로그 + 연동 진입 흐름
- [ ] 연동 성공 후 결제 재시도 가능

**서버 코드 참조**: `Framework/Framework.Api/Filters/RequireLinkedAccountAttribute.cs` (필터 구현)

---

# 6부 — 광고 SDK 통합

## 21. Unity Ads / IronSource SDK 연동

**선행 조건**: 로그인 완료, 광고 SDK 초기화 완료
**UI 필요**: "광고 시청" 버튼 (보상형 광고)
**호출 시점**: 사용자가 광고 시청 버튼 클릭

**중요 — 클라이언트는 직접 서버 호출 없음**
- 광고 시청 완료 → 광고 네트워크 서버가 우리 서버에 SSV 콜백 직접 발송
- 보상은 **우편함을 통해 자동 도착**
- 클라는 광고 SDK에 `userId` (PlayerId)만 전달하면 됨

**Unity Ads 통합**
```csharp
// 광고 시청 시 userId 전달 — 서버가 SSV로 식별
Advertisement.Show("placementId", new ShowOptions {
    resultCallback = OnAdResult,
    userMetadata = new MetaData("player") { { "userId", playerId.ToString() } }
});
```

**IronSource 통합**
```csharp
IronSource.Agent.setDynamicUserId(playerId.ToString());
IronSource.Agent.showRewardedVideo("placementId");
```

**보상 수령 흐름**
1. 광고 시청 완료
2. 광고 네트워크 서버 → 우리 서버 SSV 콜백 (HMAC-SHA256 서명 검증)
3. 서버 → 우편함에 보상 발송
4. **클라가 우편함을 새로 호출(13번)하여 표시**

**처리 권장**
- 광고 시청 완료 후 5~10초 폴링으로 우편함 새로고침 (SSV 도달 지연 대응)
- 또는 사용자가 우편함 진입 시 새로고침

**구현 인수 기준**
- [ ] 광고 SDK에 PlayerId 전달
- [ ] 시청 완료 후 우편함 새로고침
- [ ] 일일 한도 도달 시 광고 SDK 측에서 차단 (서버는 한도 초과 시 보상 미발송)

**서버 코드 참조**:
- `Framework/Framework.Api/Controllers/Player/AdsCallbackController.cs` (서버 측 SSV 핸들러 — 클라 호출 없음)
- 한도/설정: Admin `/ad-policies` 페이지에서 설정

> **참고**: 광고 SDK 자체 통합 코드는 각 SDK 공식 문서 참조. 서버 측은 PlacementId별 보상 매핑만 담당.

---

# 7부 — 부가 기능

## 22. 문의 제출 / 조회

**선행 조건**: 로그인 완료
**UI 필요**: 문의 작성 화면 + 내 문의 목록 화면

**요청 (제출)**
```http
POST /api/inquiries
Authorization: Bearer {AccessToken}
Content-Type: application/json

{ "content": "버그 신고 내용..." }
```
- `content` 1~2000자

**응답** — 201 Created (본문 없음)

**요청 (내 목록)**
```http
GET /api/inquiries
Authorization: Bearer {AccessToken}
```

**응답**
```json
[
  {
    "id": 1, "content": "버그 신고...",
    "adminReply": "답변드립니다...", "repliedAt": "2026-05-01T...",
    "createdAt": "2026-04-30T..."
  }
]
```

**에러 처리**
| Status | 사유 | UI 처리 |
|---|---|---|
| 400 | VALIDATION_FAILED | 입력 검증 (1~2000자) |
| 401 | - | 인터셉터 |

**구현 인수 기준**
- [ ] 빈 내용 제출 차단
- [ ] 답변 도착 시 알림 (`repliedAt` 갱신)

**서버 코드 참조**: `Framework/Framework.Api/Controllers/Player/InquiriesController.cs`

---

# 8부 — 공통 처리 패턴

## 23. ProblemDetails 응답 형식 (RFC 7807)

대부분의 4xx/5xx 응답은 다음 형식이다:
```json
{
  "type": "https://framework.api/errors/iap-receipt-invalid",
  "title": "IAP 영수증 검증 실패",
  "status": 409,
  "detail": "구체 사유...",
  "instance": "/api/iap/google/verify",
  "errorCode": "IAP_RECEIPT_INVALID",
  "traceId": "..."
}
```

**클라 분기 우선순위**
1. `errorCode` 필드 (있는 경우 — `24번 표` 참조)
2. HTTP `status` 코드
3. `detail` (사용자 노출 시 사용 — `title`은 디버깅용)

**예외 — 단순 형식 응답**
일부 응답은 단순 객체 형식 (점검 모드 503, IAP 403 등):
```json
{ "message": "..." }
```

---

## 24. ErrorCodes 표 (전체 카탈로그)

| ErrorCode | HTTP | 발생 시나리오 | 클라 UI 처리 |
|---|---|---|---|
| `INTERNAL_ERROR` | 500 | 서버 내부 예외 | 일반 에러 안내 + 재시도 |
| `INVALID_ENUM_VALUE` | 400 | enum 파라미터 오류 | 클라 버그 — 로그 |
| `VALIDATION_FAILED` | 400 | DTO 검증 실패 | 입력 검증 (`detail` 사용자에 표시) |
| `AD_SIGNATURE_INVALID` | 401 | 광고 SSV 서명 오류 | 클라 호출 안 함 (서버↔서버) |
| `AD_POLICY_NOT_FOUND` | 200 | 광고 정책 없음 | 보상 미지급 안내 |
| `AD_DAILY_LIMIT_EXCEEDED` | 200 | 일일 한도 초과 | "내일 다시 시도" 안내 |
| `IAP_PRODUCT_NOT_FOUND` | 404 | 상품 마스터 없음 | 운영자 문의 + 결제 환불 안내 |
| `IAP_RECEIPT_INVALID` | 409 | 영수증 위변조/이상 | 재시도 X, 운영자 문의 |
| `IAP_TOKEN_OWNERSHIP_MISMATCH` | 422 | 타 플레이어 토큰 | 보안 사고 — 강제 로그아웃 권장 |
| `IAP_VERIFIER_ERROR` | 502 | Google API 장애 | 백오프 재시도 |
| `IAP_VERIFY_CONCURRENCY_EXHAUSTED` | 503 | verify 동시성 한도 초과 | 잠시 후 재시도 (멱등) |
| `GOOGLE_ACCOUNT_CONFLICT` | 409 | 구글 계정 충돌 | 충돌 해소 다이얼로그 (5번/6번) |

> 추가될 때마다 서버 `Framework/Framework.Api/ProblemDetails/ErrorCodes.cs` 갱신.

---

## 25. Rate Limit 429 처리

**응답**
```http
HTTP/1.1 429 Too Many Requests
Retry-After: 30
```

**처리**
- `Retry-After` 헤더 값(초) 만큼 대기 후 재시도
- 헤더 없으면 5초 백오프
- 최대 3회 재시도, 초과 시 사용자에게 "요청이 많습니다" 안내

**정책 한도**
- `auth`: 미인증 IP 분당 15 / 인증 PlayerId 분당 30
- `game`: PlayerId 분당 120 (또는 IP)
- `iap-verify`: PlayerId 분당 20

**구현 인수 기준**
- [ ] 429 자동 백오프
- [ ] 한도 초과 누적 시 사용자 안내

---

## 26. 점검 모드 503 처리

(1번에서 다룸 — 미들웨어 인터셉터 패턴)

**핵심**: 모든 API 응답을 인터셉터로 감지. 503 + `{"message":"서버 점검 중..."}` 패턴 → 점검 화면 전환.

---

## 27. HTTP 타임아웃 / 재시도 권장값

| 항목 | 권장값 | 근거 |
|---|---|---|
| HTTP 타임아웃 | **≥ 60초** | 서버 DB transient 장애 시 EnableRetryOnFailure 5회×10초 백오프로 최대 50초 응답 지연 가능 |
| 재시도 가능 응답 | 502, 503, 504, 네트워크 오류 | 일시 장애 — 멱등 재시도 안전 |
| 재시도 불가 응답 | 4xx (단 429 제외) | 클라 측 오류 — 재시도해도 동일 결과 |
| IAP verify 재시도 | 최대 5회, 30초~5분 백오프 | 결제 데이터 — 멱등 보장됨 |
| 일반 API 재시도 | 최대 3회, 5~30초 백오프 | UX 균형 |

---

## 28. JWT 만료(401) 인터셉터 흐름

(8번에서 자세히 다룸)

**요약**:
```
[원 요청] → 401 → [refresh 호출] → [원 요청 재시도(1회)]
                ↓ 401
                [로그인 화면으로 이동]
```

**구현 주의**
- mutex로 동시 다발 401에 대한 refresh를 1회로 직렬화
- refresh 자체가 401이면 무한 루프 차단

---

# 부록 A — 의무 동작 체크리스트 (법적/마켓 정책)

| 항목 | 근거 | 구현 위치 |
|---|---|---|
| **탈퇴 안내 팝업** (캐릭터/아이템/우편/결제 보상 영구 손실 명시) | 개인정보보호법 §22 / Google Play UDP / Apple 5.1.1(v) | 10번 |
| **NoticeId PlayerPrefs 저장** (1회성 공지 재표시 차단) | UX | 3번 |
| **DeviceId 영구 저장** (게스트 계정 식별) | 4번 |
| **AccessToken 메모리 저장 / RefreshToken 보안 저장소** | 보안 | `부록 C` |
| **점검 모드 503 인터셉터** (전역) | 1번 |
| **JWT 만료(401) 인터셉터** (전역) | 8번/28번 |
| **Rate Limit 429 백오프** (전역) | 25번 |
| **HTTP 타임아웃 ≥ 60초** | 27번 |
| **광고 SDK userId 전달** (보상 매핑) | 21번 |
| **IAP verify 멱등 재시도** (502/503) | 19번 |

---

# 부록 B — 백엔드 미구현 영역 (가이드 작성 불가)

다음 기능은 클라이언트 가이드 작성 시점 백엔드에 **API가 없다**. 추후 별도 라운드에서 도입 예정.

| 영역 | 현재 상태 | 클라 임시 처리 |
|---|---|---|
| **인벤토리 조회 API** | Player용 엔드포인트 없음 (Admin만 존재) | 우편 수령 시 응답으로 갱신, 또는 향후 도입 대기 |
| **IAP 상품 목록 API** | Admin CRUD만. 클라용 GET 엔드포인트 없음 | Google Play Billing SDK의 `QueryProductDetailsAsync`로 직접 조회 |
| **게임 결과 기록 API** | 엔드포인트 부재 | 스테이지 클리어(17번)로 부분 대체. PvP/매치메이킹은 미구현 |
| **매치메이킹** | SignalR 허브 코드 존재하나 UX 흐름 미정 | 향후 라운드 |
| **플레이어 프로필 조회 API** | 엔드포인트 부재 | TokenResponseDto의 PlayerId/IsNewPlayer로만 구분 |

---

# 부록 C — DeviceId / IdToken / 토큰 저장 가이드

## DeviceId
- 형식: 8~64자, 영문/숫자/하이픈/언더스코어
- 발급: `SystemInfo.deviceUniqueIdentifier` 또는 자체 GUID
- 저장: PlayerPrefs (영구). 탈퇴 시 삭제
- 재설치 시: 동일 DeviceId면 동일 게스트 계정 복구

## IdToken (Google Sign-In)
- 발급: Google Sign-In SDK
- 클라이언트 ID: 서버 `Google:ClientId`와 일치 필수
- 만료: 1시간 — 매 로그인마다 새로 발급
- 저장: 저장 안 함 (즉시 서버 송신 후 폐기)

## AccessToken
- 발급: 서버 `/api/auth/*` 응답
- 만료: 약 1시간 (서버 측 결정)
- 저장: **메모리만** (앱 종료 시 사라짐 — 의도된 동작)
- 송신: 모든 인증 필요 요청에 `Authorization: Bearer {token}`

## RefreshToken
- 발급: 서버 `/api/auth/*` 응답 (AccessToken과 함께)
- 만료: 약 7일 (서버 측 결정, 갱신 가능)
- 저장: **보안 저장소** (Android Keystore / iOS Keychain) — 평문 PlayerPrefs 금지
- 갱신: refresh 호출 시 새 RefreshToken 함께 받음 → 즉시 갱신
- 무효화: 서버 측 회전(rotation) 후 이전 토큰 사용 시 401

---

# 변경 이력

| 일자 | 라운드 | 변경 |
|---|---|---|
| 2026-05-05 | round_20260503 종결 | 최초 작성 — 20개 엔드포인트 + 공통 처리 패턴 + 부록 |
