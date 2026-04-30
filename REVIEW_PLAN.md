# WebAPIFramework 종합 리뷰 계획

> 본 문서는 프로젝트 전체를 단계별로 검토하기 위한 마스터 플랜입니다.
> 각 단계는 독립적으로 실행 가능하도록 설계되어, 컨텍스트 컴팩트가 발생해도
> 해당 단계의 입력만으로 작업을 재개할 수 있습니다.

## 확정 범위 (사용자 확인)
- **리뷰 범위**: 서버측 5개 프로젝트만 (Unity 클라이언트 제외)
- **Migrations 깊이**: 스키마 일관성 + 데이터 이전 누락까지 전사 검토
- **보안 도구**: 코드 레벨 + gitleaks + dotnet list package --vulnerable 통합
- **산출물 형식**: 단일 통합 보고서 (챕터 분리)

---

## 0. 리뷰 대상 및 전체 구조

### 솔루션 레이아웃 (5계층 Clean Architecture)
| 프로젝트 | 역할 | 주요 폴더 |
|---|---|---|
| Framework.Domain | 엔티티/VO/Enum/Repository 인터페이스 | Entities, ValueObjects, Enums, Interfaces, Constants, Content |
| Framework.Application | 유스케이스/서비스/DTO | Features/{도메인}, Content/{도메인}, Common |
| Framework.Infrastructure | EF Core DbContext/Repository 구현/Migrations | Persistence, Repositories, Migrations, Content |
| Framework.Api | ASP.NET Core API (Controller/Middleware/Filter/Hub) | Controllers/{Player,Admin,Content}, Middleware, Filters, Services, Json, ProblemDetails, Hubs |
| Framework.Admin | Blazor Server 운영툴 | Components/Pages, Handlers, Http, Json, Logging |

### 의존성 방향 (위반 시 즉시 반려)
```
Api/Admin → Application → Domain ← Infrastructure
```

### 기능 모듈 (22+개)
Auth(JWT/Google), Ranking, Inventory, Mail, DailyLogin, Matchmaking, RewardFramework, Item Master, Admin Auth(X-Admin-Key), SystemConfig, RateLimit/Security, 점검모드, 계정탈퇴, 버전체크, Notice/Shout, Inquiry, AuditLog, AdReward(Unity/IronSource), UnitOfWork, IAP(Google/RTDN), Exp/Level, Stage(Content)

---

## 1단계: 아키텍처 검토 (architect)

### 목적
Clean Architecture 정합성, 레이어 책임 분리, 의존성 방향, 추상화 적정성을 거시적으로 평가.

### 입력 (재시작 시 필요한 정보)
- 본 문서 0장 (구조 요약)
- DEVNOTES.md (기능 현황 / 설계 결정)
- CLAUDE.md (코딩 규칙, 역할 분리)

### 점검 항목
| # | 항목 | 합격 기준 |
|---|---|---|
| A1 | 의존성 방향 | Domain이 Infrastructure/Application/Api를 참조하지 않음. Application이 Infrastructure를 참조하지 않음 |
| A2 | 레이어 책임 | Controller에 비즈니스 로직 없음, Service에 EF Core 직접 호출 없음, Repository에 도메인 규칙 없음 |
| A3 | 인터페이스 위치 | Repository/외부연동 인터페이스는 Domain 또는 Application, 구현은 Infrastructure |
| A4 | DTO 경계 | Domain 엔티티가 Controller 응답으로 그대로 노출되지 않음 |
| A5 | DI 등록 일관성 | Api/Program.cs와 ServiceExtensions.cs 등록 책임 분리, 누락 없음 |
| A6 | 트랜잭션 경계 | IUnitOfWork 사용 패턴 일관성 (RewardDispatcher 외 다중 쓰기 케이스) |
| A7 | Content 영역 분리 | Framework 영역에서 Content 참조 금지 |
| A8 | Strategy 패턴 적용 | AdNetwork/IapStore Resolver 패턴 일관성 |
| A9 | 인증 분리 | JWT(Player)와 X-Admin-Key(Admin)의 독립성, 미들웨어/필터 충돌 여부 |
| A10 | 횡단관심사 | Rate Limiting / 점검 모드 / 전역 예외 처리 / 로깅의 적용 일관성 |

### 집중 검토 파일
- `Framework.Api/Program.cs`, `Framework.Api/Extensions/ServiceExtensions.cs`
- `Framework.Admin/Program.cs`
- 각 csproj `<ProjectReference>`
- `Framework.Application/Features/**/*Service.cs` 전수
- `Framework.Api/Controllers/**/*.cs` 전수
- `Framework.Infrastructure/Repositories/*.cs` 전수
- `Framework.Domain/Interfaces/IUnitOfWork.cs`
- `Framework.Application/Features/Reward/RewardDispatcher.cs`
- `Framework.Api/Services/AdNetwork/AdNetworkVerifierResolver.cs`
- `Framework.Api/Services/IapStore/IapStoreVerifierResolver.cs`
- `Framework.Domain/Content/`, `Framework.Application/Content/` (Content 분리 검증)

### 산출물
- 레이어별 위반 사항 목록 (파일·라인 단위)
- 추상화 적정성 평가
- 2단계로 넘길 "구현 품질 우려" 핫스팟 리스트

### 담당 에이전트: **architect**

---

## 2단계: 구현 품질 검토 (qa-reviewer)

### 목적
코딩 규칙 준수, 한국어 주석, 네이밍 일관성, 패턴 통일성, 명백한 버그/스멜 식별.

### 입력 (재시작 시 필요한 정보)
- 본 문서 0장
- 1단계 산출물 (핫스팟 리스트)
- CLAUDE.md "Coding Rules" 섹션
- DEVNOTES.md

### 점검 항목
| # | 항목 | 합격 기준 |
|---|---|---|
| Q1 | 한국어 주석 | 파일/함수당 최소 1개 의미 있는 한국어 주석 |
| Q2 | 네이밍 | C# 표준 + 프로젝트 컨벤션 |
| Q3 | DTO 사용 | 모든 Controller 응답이 DTO |
| Q4 | Async 일관성 | async/await 누락, .Result/.Wait() 사용, CancellationToken 전파 |
| Q5 | 예외 처리 | GlobalExceptionHandler 통합 흐름 준수 |
| Q6 | EF Core 패턴 | N+1, AsNoTracking 누락, Migration 일관성 (스키마 + 데이터 이전 전사) |
| Q7 | 멱등성 | RewardGrants 선기록, 중복 호출 방어 흐름 |
| Q8 | Soft Delete | IsDeleted 쿼리 일관 적용 |
| Q9 | Razor 컴포넌트 | DirtyGuardBase / SafeComponentBase / SafeErrorBoundary 일관 사용 |
| Q10 | Magic String | enum/상수화 미흡 영역 |

### 집중 검토 영역
- `Framework.Application/Features/**/*Service.cs` 전수
- `Framework.Api/Controllers/**/*.cs` 전수
- `Framework.Infrastructure/Repositories/*.cs` 전수
- `Framework.Infrastructure/Migrations/*.cs` 전수 (스키마 일관성 + 데이터 이전 누락)
- `Framework.Infrastructure/Persistence/AppDbContext.cs`
- `Framework.Admin/Components/Pages/**/*.razor.cs` 전수

### 담당 에이전트: **qa-reviewer**

---

## 3단계: 보안 검토 (security-master)

### 목적
인증/인가, 입력 검증, DB 쿼리 안전성, 시크릿 관리, 외부 호출 검증, 로깅 민감정보 점검.
**보안 도구**: gitleaks + dotnet list package --vulnerable 결과 통합.

### 입력 (재시작 시 필요한 정보)
- 본 문서 0장
- 1·2단계 산출물
- DEVNOTES.md "[필수] appsettings.json 교체값"

### 점검 항목
| # | 항목 |
|---|---|
| S1 | JWT 흐름 — SecretKey 길이/검증, AccessToken 유효기간, RefreshToken 회전 |
| S2 | 인가 — `[Authorize]`/`[AdminApiKey]`/`[RequireLinkedAccount]` 누락 엔드포인트 |
| S3 | 디버그 우회 — `#if DEBUG` 블록 Release 제외 재확인 |
| S4 | 입력 검증 — DTO ModelState/Annotation, 파라미터 범위 |
| S5 | SQL Injection — LINQ/파라미터화, FromSqlRaw/ExecuteSqlRaw |
| S6 | IDOR — PlayerId 기반 자원 접근 본인 확인 |
| S7 | Rate Limiting — 추가 보호 필요 엔드포인트 |
| S8 | 시크릿 관리 — appsettings 평문 시크릿, .gitignore 설정 |
| S9 | 외부 검증 — Google IdToken / Unity Ads HMAC / IronSource HMAC / Google Play Receipt / RTDN OIDC |
| S10 | OIDC RTDN — GooglePubSubAuthenticator JWT 검증, Audience, replay 방지 |
| S11 | 로깅 민감정보 — JWT, Token, Receipt, DeviceId 평문 로깅 |
| S12 | CORS / 보안 헤더 — 허용 Origin, HSTS, Swagger 운영 노출 |
| S13 | 보상 멱등 — 동시 클레임 ConcurrencyToken, 중복 지급 방어 |
| S14 | Admin 보호 — X-Admin-Key 타이밍 공격, 갱신 절차 |
| S15 | 개인정보 — 계정 탈퇴 CASCADE 범위, 보관기간 |
| S16 | **gitleaks** — 시크릿/토큰 하드코딩 이력 |
| S17 | **dotnet list package --vulnerable** — 알려진 취약점 패키지 |

### 담당 에이전트: **security-master**

---

## 4. 단계 간 인계 규칙

각 단계 종료 시 다음 형식 인계 노트 작성:
```
### 인계 노트 (N단계 → N+1단계)
- 검토 완료 모듈:
- 식별된 이슈 요약 (심각도 분포):
- N+1단계가 우선 봐야 할 핫스팟:
- 의문점/미해결 질문:
```

---

## 5. 실행 순서

```
[1단계 architect] → [2단계 qa-reviewer] → [3단계 security-master]
```

---

## 6. 종합 보고서 양식 (단일 파일 REVIEW_REPORT.md)

```
# WebAPIFramework 종합 리뷰 보고서

## 1장. 아키텍처 검토 결과
## 2장. 구현 품질 검토 결과
## 3장. 보안 검토 결과
## 4장. Critical Issues (즉시 조치)
## 5장. High Issues (다음 스프린트)
## 6장. Medium Issues (백로그 등록)
## 7장. Low / 추적 항목
## 8장. DEVNOTES.md 갱신 권고
```
