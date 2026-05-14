# /doc-sync — 솔루션 전수 스캔 → 문서 최신화 (Phase × Chunk, 컴팩트 내성)

솔루션 전체 코드를 청크별로 빠짐없이 읽고, 지정된 .md 문서를 코드 실제 상태와 일치하도록 추가/삭제/수정한다.
컨텍스트 컴팩트가 발생해도 STATUS.md 하나만 있으면 마지막 미완료 청크부터 재개 가능하다.

---

## 사용법

| 인자 | 동작 |
|---|---|
| `/doc-sync` | 기본 대상 3종으로 신규 라운드 시작: `DEVNOTES.md` + `SERVER_GUIDE.md` + `CLIENT_GUIDE.md` |
| `/doc-sync devnotes` | `DEVNOTES.md`만 동기화 |
| `/doc-sync server` | `SERVER_GUIDE.md`만 동기화 |
| `/doc-sync client` | `CLIENT_GUIDE.md`만 동기화 |
| `/doc-sync devnotes server` | 공백 구분 복수 지정 |
| `/doc-sync resume` | STATUS.md의 첫 미완료 청크부터 재개 |
| `/doc-sync status` | 현재 라운드 진행 상태 출력 |

`$ARGUMENTS`를 위 표 기준으로 파싱한다. 인자 없으면 3종 전체.

---

## 산출 디렉토리 정책

- 라운드 루트: `doc-sync/round_{YYYYMMDD}/`
- 현재 라운드 포인터: `doc-sync/CURRENT_ROUND.txt` — 1줄(라운드 폴더명만)
- 청크 산출물: `doc-sync/round_{YYYYMMDD}/c{N}_{slug}.md`
- 진행 추적: `doc-sync/round_{YYYYMMDD}/STATUS.md`
- `doc-sync/`는 `.gitignore`에 등재 권장

---

## 대상 문서 경로

| 키 | 경로 |
|---|---|
| `devnotes` | `DEVNOTES.md` |
| `server` | `SERVER_GUIDE.md` |
| `client` | `CLIENT_GUIDE.md` |

---

## 신규 라운드 초기화 절차 (`/doc-sync [대상]`)

1. 오늘 날짜로 `ROUND_ID = round_{YYYYMMDD}` 결정. 같은 날 재시작 시 기존 폴더 재사용 여부 1회 확인.
2. `doc-sync/{ROUND_ID}/` 생성.
3. `doc-sync/{ROUND_ID}/STATUS.md` 생성 — 대상 문서에 필요한 청크 전체를 `[ ] PENDING`으로 초기화.
4. `doc-sync/CURRENT_ROUND.txt`를 `{ROUND_ID}` 한 줄로 갱신.
5. C1부터 순차 실행.

---

## 재개 절차 (컴팩트 후 또는 `/doc-sync resume`)

다음 파일만 읽으면 재개 가능 (in-memory 변수 사용 금지):

1. `doc-sync/CURRENT_ROUND.txt` → `{ROUND_ID}`
2. `doc-sync/{ROUND_ID}/STATUS.md` → 첫 `[ ]`/`[~]` 청크 식별
3. 해당 청크 직전 산출물 1개

알고리즘:
- STATUS.md에서 미완료 첫 청크 X 식별
- X의 직전 산출물을 읽어 컨텍스트 복구
- X 청크 에이전트 호출

---

## 청크 카탈로그

### 스캔 Phase — 코드 읽기 (general-purpose 에이전트)

각 스캔 청크는 파일을 읽고 구조화된 정보를 산출물에 기록한다.
**Glob으로 파일 목록을 얻은 뒤 목록의 모든 파일을 Read로 읽는다. 샘플링·추측 금지.**

#### C1 — API 엔드포인트 전수
- **스캔 대상** (전수 읽기):
  - `Framework/Framework.Api/Controllers/` 하위 모든 `.cs`
  - `Framework/Framework.Api/Program.cs`
  - `Framework/Framework.Api/Extensions/ServiceExtensions.cs`
- **산출 정보**: 모든 엔드포인트(HTTP 메서드·경로·인증방식·요청파라미터·응답형태), Rate Limit 적용 여부, `#if DEBUG` 블록 파일 목록
- **산출물**: `doc-sync/{ROUND_ID}/c1_controllers.md`
- **관련 대상**: server, client

#### C2 — DTO · 에러코드 전수
- **스캔 대상** (전수 읽기):
  - `Framework/Framework.Api/` 하위 `*Dto.cs`, `*Request.cs`, `*Response.cs`
  - `Framework/Framework.Application/Features/` 하위 `*Dto.cs`
  - `Framework/Framework.Domain/` 하위 `*Exception*.cs`
  - `Framework/Framework.Api/ProblemDetails/` 전수
- **산출 정보**: 각 DTO 필드명·타입·어노테이션, 에러코드↔HTTP 상태코드 전체 목록
- **산출물**: `doc-sync/{ROUND_ID}/c2_dto_errors.md`
- **관련 대상**: server, client

#### C3 — 도메인 엔티티 · Enum · 최신 스키마
- **스캔 대상** (전수 읽기):
  - `Framework/Framework.Domain/Entities/` 하위 모든 `.cs`
  - `Framework/Framework.Domain/Enums/` 하위 모든 `.cs`
  - `Framework/Framework.Infrastructure/Repositories/` 하위 모든 `.cs`
  - `Framework/Framework.Infrastructure/Migrations/` — 가장 최신 마이그레이션 파일 (날짜 기준 정렬 후 최신 1개)
- **산출 정보**: 엔티티 목록·주요 필드, Enum 목록·값, 현행 DB 테이블/컬럼
- **산출물**: `doc-sync/{ROUND_ID}/c3_domain_schema.md`
- **관련 대상**: server, devnotes

#### C4 — Application 서비스 · 백그라운드 · 공통
- **스캔 대상** (전수 읽기):
  - `Framework/Framework.Application/Features/` 하위 모든 `*Service.cs`
  - `Framework/Framework.Application/BackgroundServices/` 전수
  - `Framework/Framework.Application/Common/` 전수
- **산출 정보**: 각 서비스 public 메서드 시그니처·역할, 백그라운드 서비스 스케줄·동작, RewardDispatcher 등 공통 유틸리티 동작
- **산출물**: `doc-sync/{ROUND_ID}/c4_services.md`
- **관련 대상**: server, devnotes

#### C5 — Admin Blazor 페이지 전수
- **스캔 대상** (전수 읽기):
  - `Framework/Framework.Admin/Components/Pages/` 하위 모든 `.razor`, `.razor.cs`
- **산출 정보**: Admin 페이지 목록·기능(CRUD 항목·검색/필터), 각 페이지에서 호출하는 API
- **산출물**: `doc-sync/{ROUND_ID}/c5_admin_pages.md`
- **관련 대상**: server

#### C6 — 테스트 전수
- **스캔 대상** (전수 읽기):
  - `Framework/Framework.Tests/` 하위 모든 `.cs`
- **산출 정보**: 테스트 폴더 구조·클래스·케이스 수, 커버리지 공백
- **산출물**: `doc-sync/{ROUND_ID}/c6_tests.md`
- **관련 대상**: server

#### C7 — 클라이언트 코드 전수 (CLIENT_GUIDE 대상 시에만)
- **스캔 대상** (전수 읽기):
  - `GameClient/UnityClient/Assets/` 하위 모든 `.cs` (Library/PackageCache 제외)
- **산출 정보**: 실제 사용 중인 API 엔드포인트, 클라이언트 측 DTO·필드, 에러코드 처리 분기
- **산출물**: `doc-sync/{ROUND_ID}/c7_client.md`
- **관련 대상**: client

---

### 동기화 Phase — 문서 수정 (doc-sync 에이전트, run_in_background: true)

스캔 청크가 모두 완료된 후, 대상 문서별로 doc-sync 에이전트를 호출한다.

#### S1 — SERVER_GUIDE.md 동기화 (server 대상 시)
- **입력 청크**: c1, c2, c3, c4, c5, c6 산출물
- **산출물**: `doc-sync/{ROUND_ID}/s1_server_result.md` (변경 항목 목록)

#### S2 — CLIENT_GUIDE.md 동기화 (client 대상 시)
- **입력 청크**: c1, c2, c7 산출물
- **산출물**: `doc-sync/{ROUND_ID}/s2_client_result.md`

#### S3 — DEVNOTES.md 동기화 (devnotes 대상 시)
- **입력 청크**: c3, c4 산출물
- **산출물**: `doc-sync/{ROUND_ID}/s3_devnotes_result.md`

---

## 청크 실행 표준 절차

1. STATUS.md 해당 청크 행을 `[~] IN_PROGRESS` + 시작 시각으로 갱신
2. 스캔 청크: general-purpose 에이전트를 **백그라운드**로 호출
   동기화 청크: doc-sync 에이전트를 **백그라운드**로 호출
3. 에이전트 종료 후 산출물 파일 존재 검증
4. STATUS.md를 `[x] DONE` + 완료 시각으로 갱신
5. 다음 청크 진행
6. 실패 시 `[!] FAILED` + 사유 1줄 기록, 사용자에게 보고. 자동 재시도 금지

---

## STATUS.md 형식

```
대상 문서: server, client, devnotes
ROUND_ID: round_{YYYYMMDD}

| 청크 | 설명 | 상태 | 산출물 | 시작 | 완료 |
|---|---|---|---|---|---|
| C1 | API 엔드포인트 | [ ] PENDING | c1_controllers.md | | |
...
```

상태 코드: `[ ] PENDING` / `[~] IN_PROGRESS` / `[x] DONE` / `[!] FAILED`

---

## 스캔 청크 에이전트 프롬프트 필수 포함 항목

- 스캔 대상 파일 경로 (카탈로그 그대로)
- "Glob으로 파일 목록 확보 후 **목록의 모든 파일**을 Read로 읽을 것. 샘플링·추측 금지"
- 산출물 경로와 형식 (마크다운 표 또는 목록)
- "코드 파일 수정 절대 금지. 산출물 .md만 쓰기 허용"
- ROUND_ID

## 동기화 청크 에이전트 프롬프트 필수 포함 항목

- 입력 청크 산출물 경로 목록
- 대상 .md 파일 경로
- 비교 기준 (추가/삭제/수정 판단 기준)
- 결과 산출물 경로
- "문서 전체 구조(목차·섹션 순서) 유지, 내용만 수정"
- "코드 파일 수정 절대 금지"

---

## 비교 기준 (동기화 청크 공통)

- 문서 기술(API 경로·파라미터·응답 형식·상태코드 등)이 코드와 다르면 → **수정**
- 문서에 있으나 코드에 없으면 → **삭제**
- 코드에 있으나 문서에 없으면 → **추가**
- DEVNOTES `[미구현]` 섹션: 코드에 이미 구현된 항목 → **삭제** (신규 미구현 항목 발굴은 이 스킬 범위 밖)
- 설계 의도·운영 정책·주의사항 등 코드로 검증 불가한 내용 → **건드리지 않음**

---

## 주의사항

- in-memory 변수 사용 금지. 모든 인계는 산출물 파일을 통해
- 중간 청크 완료는 사용자에게 보고하지 않음. **최종 완료 보고만 필수**
- 컴팩트 발생 시 `/doc-sync resume`만으로 재개 가능해야 함 — 본 스킬의 핵심 설계 목표
- 대상 문서에 불필요한 청크는 실행하지 않음 (예: client 미대상 시 C7 건너뜀)
