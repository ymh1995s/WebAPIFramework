# Framework.Api K8s 부하테스트 환경

## 개요

부하테스트 **시나리오 3(CPU 집약)** 및 **시나리오 4(DB 집약)** 측정을 위한 로컬 K8s 클러스터 구성.

- kind 클러스터 (control-plane 1 + worker 3)
- Framework.Api Deployment (LoadTest 빌드 — 인증 우회)
- NodePort 서비스 (localhost:30080)
- HPA (CPU 70% 임계값, maxReplicas=4)

---

## 사전 설치

```powershell
# kind 설치
winget install Kubernetes.kind

# kubectl 설치
winget install Kubernetes.kubectl
```

Docker Desktop 이 실행 중이어야 합니다.

---

## Docker Desktop 자원 설정

HPA 스케일 아웃 시 파드가 최대 4개까지 생성됩니다. 충분한 자원 확보를 권장합니다.

Settings → Resources:
- CPU: **6코어 이상** 권장
- Memory: **8GB 이상** 권장

---

## 실행 순서

| 번호 | 스크립트 | 설명 |
|------|----------|------|
| 01 | `01-create-cluster.ps1` | kind 클러스터 생성 (control-plane + worker 3) |
| 02 | `02-build-image.ps1` | LoadTest 이미지 빌드 → kind 클러스터에 로드 |
| 03 | `03-install-metrics-server.ps1` | HPA용 metrics-server 설치 및 TLS 우회 패치 |
| 04 | `04-deploy.ps1` | Deployment/Service/HPA 적용 |
| 05 | `05-verify.ps1` | 엔드포인트 동작 및 상태 검증 |

```powershell
# scripts/ 디렉토리에서 순서대로 실행
.\01-create-cluster.ps1
.\02-build-image.ps1
.\03-install-metrics-server.ps1
.\04-deploy.ps1
.\05-verify.ps1
```

---

## deployment.yaml POSTGRES_PASSWORD 치환

`manifests/deployment.yaml` 에서 `<POSTGRES_PASSWORD>` 를 실제 Postgres 패스워드로 교체해야 합니다.

```yaml
# 변경 전
value: "Host=host.docker.internal;Port=5432;Database=framework_db;Username=postgres;Password=<POSTGRES_PASSWORD>"

# 변경 후 (예시: 패스워드가 postgres 인 경우)
value: "Host=host.docker.internal;Port=5432;Database=framework_db;Username=postgres;Password=postgres"
```

**04-deploy.ps1 실행 전에 반드시 치환할 것.** 치환하지 않으면 파드가 DB 연결 오류로 CrashLoopBackOff 상태가 됩니다.

---

## 시나리오 3/4 측정 방법

### 사전 준비

별도 콘솔에서 파드 수 변화를 실시간 관찰합니다.

```powershell
# 콘솔 1: 파드 증감 실시간 관찰
kubectl get pods -w
```

### 시나리오 3 — CPU 집약 (HPA 스케일 아웃 검증)

```powershell
# 콘솔 2: k6 실행
$env:K6_WEB_DASHBOARD = "true"
$env:K6_WEB_DASHBOARD_EXPORT = "C:\Users\user\Documents\WebAPIFramework\loadtest\results\03-k8s-cpu.html"
k6 run C:\Users\user\Documents\WebAPIFramework\loadtest\scenarios\03-cpu-k8s.js
```

### 시나리오 4 — DB 집약

```powershell
# 콘솔 2: k6 실행
$env:K6_WEB_DASHBOARD = "true"
$env:K6_WEB_DASHBOARD_EXPORT = "C:\Users\user\Documents\WebAPIFramework\loadtest\results\04-k8s-db.html"
k6 run C:\Users\user\Documents\WebAPIFramework\loadtest\scenarios\04-db-k8s.js
```

### HPA 상태 모니터링

```powershell
# 부하 중 HPA 상태 확인 (CPU 수치 및 레플리카 수)
kubectl get hpa -w
```

---

## 트러블슈팅

### `host.docker.internal` 로 DB에 도달하지 못할 때

kind 컨테이너에서 `host.docker.internal` DNS가 동작하지 않는 경우:

1. 호스트 머신의 LAN IP 확인: `ipconfig` → IPv4 주소 (예: `192.168.1.100`)
2. `manifests/deployment.yaml` ConnectionStrings 에서 `host.docker.internal` 을 해당 IP로 교체
3. `04-deploy.ps1` 재실행

### Pod CrashLoopBackOff

```powershell
# 파드 이름 확인
kubectl get pods

# 로그 확인 (DB 연결 오류 가능성)
kubectl logs <pod-name>
```

DB 연결 실패가 원인이라면 `<POSTGRES_PASSWORD>` 치환 여부 및 Postgres 실행 상태를 확인하세요.

### HPA CPU 수치가 `<unknown>` 으로 표시될 때

metrics-server 가 첫 메트릭 수집을 완료하는 데 30~60초가 걸립니다. 잠시 대기 후 재확인하세요.

```powershell
# 30초 후 재확인
kubectl get hpa
```

그래도 해결되지 않으면 `03-install-metrics-server.ps1` 을 다시 실행하세요.

---

## 클러스터 제거

```powershell
.\99-cleanup.ps1
```

클러스터 삭제 후 재구성하려면 01번부터 다시 실행합니다.
