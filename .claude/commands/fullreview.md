# /fullreview — WebAPIFramework 종합 리뷰

WebAPIFramework 프로젝트를 **3단계 순차 검토**로 전사 분석하고 단일 보고서(`REVIEW_REPORT.md`)를 생성한다.

- 1단계: architect → 아키텍처 검토
- 2단계: qa-reviewer → 구현 품질 검토
- 3단계: security-master → 보안 검토

각 단계는 독립 실행 가능하도록 설계되어 있어 컨텍스트 컴팩트 발생 시에도 재개할 수 있다.

---

## 실행 전 확인사항

`$ARGUMENTS`가 있으면 해당 단계만 실행한다 (예: `/fullreview 3` → 3단계만).
인자가 없으면 1→2→3 전체를 실행한다.

---

## 공통 컨텍스트

**프로젝트 위치**: `C:\Users\user\Documents\WebAPIFramework`

**5계층 구조**
- `Framework.Domain`: 엔티티/VO/Enum/Repository 인터페이스
- `Framework.Application`: 유스케이스/서비스/DTO
- `Framework.Infrastructure`: EF Core DbContext/Repository/Migrations
- `Framework.Api`: ASP.NET Core Web API
- `Framework.Admin`: Blazor Server 운영툴

**올바른 의존성 방향**: `Api/Admin → Application → Domain ← Infrastructure`

---

## 1단계 — 아키텍처 검토 (architect)

아래 프롬프트로 **architect** 에이전트를 실행한다. 코드 수정 없음.

```
WebAPIFramework 프로젝트의 아키텍처 검토를 수행해줘. 설계 문서 생성 목적이며, 코드 수정 절대 금지.

## 점검 항목
| # | 항목 | 합격 기준 |
|---|---|---|
| A1 | 의존성 방향 | 각 csproj ProjectReference 전수 확인. Domain이 상위 레이어를 참조하지 않음 |
| A2 | 레이어 책임 | Controller에 비즈니스 로직 없음, Service에 EF Core 직접 호출 없음, Repository에 도메인 규칙 없음 |
| A3 | 인터페이스 위치 | Repository/외부연동 인터페이스는 Domain 또는 Application, 구현은 Infrastructure |
| A4 | DTO 경계 | Domain 엔티티가 Controller 응답으로 그대로 노출되지 않음 |
| A5 | DI 등록 일관성 | Program.cs와 ServiceExtensions 등록 책임 분리, 누락 없음 |
| A6 | 트랜잭션 경계 | IUnitOfWork 사용 패턴 일관성. 중첩 BeginTransactionAsync 여부 검사 |
| A7 | Content 영역 분리 | Framework 영역이 Content 영역을 참조하지 않음 |
| A8 | Strategy 패턴 | AdNetwork/IapStore Resolver 패턴 일관성 |
| A9 | 인증 분리 | JWT(Player)와 X-Admin-Key(Admin) 독립성, 충돌 여부 |
| A10 | 횡단관심사 | Rate Limiting / 점검 모드 / 전역 예외 처리 / 로깅 적용 일관성 |

## 집중 검토 파일
- 각 프로젝트 .csproj 파일
- Framework.Api/Program.cs, Extensions/ServiceExtensions.cs
- Framework.Admin/Program.cs
- Framework.Application/Features/**/*Service.cs 전수
- Framework.Api/Controllers/**/*.cs 전수
- Framework.Infrastructure/Repositories/*.cs 전수
- Framework.Domain/Interfaces/IUnitOfWork.cs + Framework.Infrastructure/Persistence/UnitOfWork.cs
- Framework.Application/Features/Reward/RewardDispatcher.cs
- Framework.Api/Services/AdNetwork/, Framework.Api/Services/IapStore/

## 산출물
- 위반 항목: 파일·라인·심각도(Critical/High/Med/Low)
- 추상화 적정성 평가
- 2단계 핫스팟 리스트
- 인계 노트 형식으로 정리

인계 노트 형식:
### 인계 노트 (1단계 → 2단계)
- 검토 완료 모듈:
- 식별된 이슈 요약 (심각도 분포):
- 2단계가 우선 봐야 할 핫스팟:
- 의문점/미해결 질문:
```

1단계 완료 후 architect 결과를 **PHASE1_FINDINGS** 변수로 기억한다.

---

## 2단계 — 구현 품질 검토 (qa-reviewer)

1단계 결과(PHASE1_FINDINGS)를 포함하여 **qa-reviewer** 에이전트를 실행한다. 코드 수정 없음.

```
WebAPIFramework 프로젝트의 구현 품질 검토를 수행해줘. 코드 수정 절대 금지.

## 1단계에서 넘어온 핫스팟
[PHASE1_FINDINGS의 핫스팟 리스트 삽입]

## 점검 항목
| # | 항목 | 합격 기준 |
|---|---|---|
| Q1 | 한국어 주석 | 파일/함수당 최소 1개 의미 있는 한국어 주석 (CLAUDE.md 강제 규칙) |
| Q2 | 네이밍 | C# 표준 + 프로젝트 컨벤션 (I접두 인터페이스, Async 접미, DTO/Service/Repository 접미) |
| Q3 | DTO 사용 | 모든 Controller 응답이 DTO |
| Q4 | Async 일관성 | async/await 누락, .Result/.Wait() 사용, CancellationToken 전파 |
| Q5 | 예외 처리 | GlobalExceptionHandler 통합 흐름 준수 |
| Q6 | EF Core 패턴 | N+1, AsNoTracking 누락, Migration 스키마+데이터 이전 전사 검토 |
| Q7 | 멱등성 | RewardGrants 선기록, 중복 호출 방어 흐름 전 보상 경로 점검 |
| Q8 | Soft Delete | IsDeleted 쿼리 일관 적용 |
| Q9 | Razor 컴포넌트 | DirtyGuardBase / SafeComponentBase / SafeErrorBoundary 일관 사용 |
| Q10 | Magic String | enum/상수화 미흡 영역 |

## 집중 검토 영역
- Framework.Application/Features/**/*Service.cs 전수
- Framework.Api/Controllers/**/*.cs 전수
- Framework.Infrastructure/Repositories/*.cs 전수
- Framework.Infrastructure/Migrations/*.cs 전수 (스키마 + 데이터 이전 누락)
- Framework.Infrastructure/Persistence/AppDbContext.cs
- Framework.Admin/Components/Pages/**/*.razor.cs 전수

## 인계 노트 형식
### 인계 노트 (2단계 → 3단계)
- 검토 완료 모듈:
- 식별된 이슈 요약 (심각도 분포):
- 3단계가 우선 봐야 할 보안 우려 후보:
- 의문점/미해결 질문:
```

2단계 완료 후 qa-reviewer 결과를 **PHASE2_FINDINGS** 변수로 기억한다.

---

## 3단계 — 보안 검토 (security-master)

1·2단계 결과를 포함하여 **security-master** 에이전트를 실행한다. 코드 수정 없음.

```
WebAPIFramework 프로젝트의 보안 검토를 수행해줘. 코드 수정 절대 금지.

## 이전 단계 보안 우려 후보
[PHASE2_FINDINGS의 보안 우려 후보 삽입]

## 점검 항목
| # | 항목 |
|---|---|
| S1 | JWT 흐름 — SecretKey 길이, AccessToken 유효기간, RefreshToken 회전 |
| S2 | 인가 — [Authorize]/[AdminApiKey]/[RequireLinkedAccount] 누락 엔드포인트 |
| S3 | 디버그 우회 — #if DEBUG 블록 Release 제외 재확인 |
| S4 | 입력 검증 — DTO ModelState/Annotation, 파라미터 범위 |
| S5 | SQL Injection — LINQ/파라미터화, FromSqlRaw/ExecuteSqlRaw |
| S6 | IDOR — PlayerId 기반 자원 접근 본인 확인 |
| S7 | Rate Limiting — 추가 보호 필요 엔드포인트 |
| S8 | 시크릿 관리 — appsettings 평문 시크릿, .gitignore 설정 |
| S9 | 외부 검증 — Google IdToken / Unity Ads HMAC / IronSource HMAC / Google Play Receipt / RTDN OIDC |
| S10 | OIDC RTDN — JWT 검증, Audience, replay 방지 |
| S11 | 로깅 민감정보 — JWT, Token, Receipt, DeviceId 평문 로깅 |
| S12 | CORS / 보안 헤더 — 허용 Origin, HSTS, Swagger 운영 노출 |
| S13 | 보상 멱등 — 동시 클레임 ConcurrencyToken, 중복 지급 방어 |
| S14 | Admin 보호 — X-Admin-Key 타이밍 공격, 갱신 절차 |
| S15 | 개인정보 — 계정 탈퇴 CASCADE 범위, 보관기간 |
| S16 | gitleaks/grep — appsettings, .env 등 시크릿 하드코딩 이력 |
| S17 | dotnet list package --vulnerable — 취약 패키지 확인 (Framework/ 디렉토리에서 실행) |

## 보안 도구
- gitleaks가 있으면: gitleaks detect --source . --no-git
- gitleaks 없으면: appsettings*.json, .env 파일 grep으로 대체
- dotnet list package --vulnerable 실행

## 집중 검토 파일
- Framework.Api/Program.cs, Extensions/ServiceExtensions.cs
- Framework.Api/Filters/AdminApiKeyAttribute.cs, RequireLinkedAccountAttribute.cs
- Framework.Api/Services/JwtTokenProvider.cs, GoogleTokenVerifier.cs
- Framework.Application/Features/Auth/AuthService.cs
- Framework.Api/Services/AdNetwork/UnityAdsVerifier.cs, IronSourceVerifier.cs
- Framework.Api/Services/IapStore/GooglePlayStoreVerifier.cs, GooglePubSubAuthenticator.cs
- Framework.Api/Controllers/Player/*.cs 전수, Admin/*.cs 전수
- Framework.Api/Hubs/MatchMakingHub.cs
- Framework.Api/appsettings*.json, Framework.Admin/appsettings*.json
- .gitignore, Framework.Infrastructure/Persistence/AppDbContext.cs

## 산출물
- 위협 등급별 이슈 목록 (Critical/High/Med/Low)
- 즉시 패치 vs 추적 관리 분류
```

---

## 최종 보고서 작성

3단계 모두 완료 후 아래 구조로 `REVIEW_REPORT.md`를 작성하고 저장한다.

```markdown
# WebAPIFramework 종합 리뷰 보고서

## Executive Summary
## 1장. 아키텍처 검토 결과
## 2장. 구현 품질 검토 결과
## 3장. 보안 검토 결과
## 4장. Critical Issues (즉시 조치)
## 5장. High Issues (다음 스프린트)
## 6장. Medium Issues (백로그 등록)
## 7장. Low / 추적 항목
## 8장. DEVNOTES.md 갱신 권고
```

보고서 저장 전에 반드시 사용자에게 확인한다:
"REVIEW_REPORT.md를 저장해도 될까요?"

---

## 주의사항

- 각 에이전트는 **읽기 전용**. 코드 수정 없음
- 인계 노트를 통해 단계 간 컨텍스트 전달
- 컨텍스트 컴팩트 발생 시: REVIEW_PLAN.md + 직전 인계 노트 + CLAUDE.md/DEVNOTES.md만으로 재시작 가능
- 중간 과정은 유저에게 보고하지 않고, 최종 보고서만 전달
