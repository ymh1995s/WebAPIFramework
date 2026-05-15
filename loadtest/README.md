# 부하테스트 (k6)

Framework.Api 부하테스트 스크립트 모음.

## 시나리오 매트릭스

| # | 파일 | 부하 종류 | 환경 | 기본 URL | 시드 필요 |
|---|---|---|---|---|---|
| 01 | `scenarios/01-cpu-no-k8s.js` | CPU 바운드 (PBKDF2) | 단일 서버 | localhost:5058 | 불필요 |
| 02 | `scenarios/02-db-no-k8s.js` | DB 바운드 (랭킹 조회) | 단일 서버 | localhost:5058 | 필요 |
| 03 | `scenarios/03-cpu-k8s.js` | CPU 바운드 (PBKDF2) | K8s 분산 | localhost:30080 | 불필요 |
| 04 | `scenarios/04-db-k8s.js` | DB 바운드 (랭킹 조회) | K8s 분산 | localhost:30080 | 필요 |

## 부하 단계 (공통)

| 단계 | 시간 | VU |
|---|---|---|
| 워밍업 | 1분 | 0 → 10 |
| 점증 1 | 2분 | 10 → 50 |
| 점증 2 | 2분 | 50 → 100 |
| 점증 3 | 2분 | 100 → 200 |
| 피크 | 3분 | 200 → 400 |
| 쿨다운 | 1분 | 400 → 0 |

## 통과 기준 (공통)

| 지표 | 기준 |
|---|---|
| p95 응답시간 | 2,000ms 이하 |
| p99 응답시간 | 5,000ms 이하 |
| 에러율 | 5% 미만 |

## 사전 준비

### 1. k6 설치

```bash
# Windows (winget)
winget install k6 --source winget

# 또는 공식 다운로드: https://k6.io/docs/getting-started/installation/
k6 version
```

### 2. API 서버 실행

## 빌드 구성 (중요)
부하테스트는 **Release 빌드 + LOADTEST 심볼** 조합으로 실행한다.
- Release 최적화 적용 (JIT, 인라이닝 등) → 운영 빌드 성능 반영
- LOADTEST 심볼로 LoadTestController 활성 + Rate Limiter 우회
- 기본 Release 빌드(심볼 미박음)는 부하테스트 코드 미포함 → 운영 보안 유지

```powershell
cd Framework/Framework.Api
dotnet run -c Release -p:DefineConstants=LOADTEST
# http://localhost:5058 에서 대기
```

참고 — 시나리오 02/04(DB 바운드)는 PlayerId=1 인증 우회가 필요하다. 해당 시나리오 실행 시 Debug 빌드 또는 별도 토큰 발급을 사용할 것:
```powershell
# Debug 빌드 (인증 우회 포함, JIT 최적화 없음 — 성능 측정 목적엔 부적합)
dotnet run --project Framework.Api --configuration Debug
```

### 3. 시드 데이터 적용 (시나리오 02, 04 전용)

시드 적용 방법은 `seed/README.md` 참조.

## 실행 명령

### 기본 실행

```bash
# 시나리오 01 — CPU 단일 서버
k6 run loadtest/scenarios/01-cpu-no-k8s.js

# 시나리오 02 — DB 단일 서버
k6 run loadtest/scenarios/02-db-no-k8s.js

# 시나리오 03 — CPU K8s
k6 run loadtest/scenarios/03-cpu-k8s.js

# 시나리오 04 — DB K8s
k6 run loadtest/scenarios/04-db-k8s.js
```

### Web Dashboard + HTML 리포트 내보내기

```bash
# Web Dashboard (실시간 브라우저 모니터링, localhost:5665)
K6_WEB_DASHBOARD=true k6 run loadtest/scenarios/01-cpu-no-k8s.js

# HTML 리포트 파일 저장 (결과 비교용)
K6_WEB_DASHBOARD=true K6_WEB_DASHBOARD_EXPORT=loadtest/results/01-cpu-no-k8s.html \
  k6 run loadtest/scenarios/01-cpu-no-k8s.js
```

### 환경변수 오버라이드

```bash
# 대상 URL 변경 (K8s 포트포워딩 등)
BASE_URL=http://localhost:8080 k6 run loadtest/scenarios/03-cpu-k8s.js

# PBKDF2 반복 횟수 변경 (시나리오 01, 03)
ITERATIONS=100000 k6 run loadtest/scenarios/01-cpu-no-k8s.js
```

## 결과 해석

### 핵심 지표

| 지표 | 설명 | 주목 상황 |
|---|---|---|
| `http_req_duration p(95)` | 95% 요청 응답시간 | 2,000ms 초과 시 병목 |
| `http_req_duration p(99)` | 99% 요청 응답시간 | 5,000ms 초과 시 타임아웃 위험 |
| `http_req_failed rate` | 에러율 (4xx/5xx) | 5% 초과 시 서버 과부하 |
| `http_reqs rate` | 초당 처리 요청 수 | K8s 파드 추가 시 증가 여부 확인 |
| `vus` | 현재 활성 VU 수 | 단계 전환 확인용 |

### K8s 시나리오 병행 관찰 명령

```bash
# HPA 자동 스케일링 실시간 관찰
kubectl get hpa -w

# 파드 분산 상태 실시간 관찰
kubectl get pods -o wide -w

# 파드별 로그 실시간 확인
kubectl logs -l app=framework-api -f
```

### 결과 파일 보관

HTML 리포트(`loadtest/results/`)는 `.gitignore`에 등록되어 로컬에만 보관됩니다.
비교 분석이 필요한 경우 결과 수치를 별도 문서에 정리하여 커밋하세요.
