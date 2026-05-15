# 04-deploy.ps1
# deployment.yaml, service.yaml, hpa.yaml 을 클러스터에 적용
# 실행 전 deployment.yaml 의 <POSTGRES_PASSWORD> 를 실제 값으로 치환했는지 확인

$ErrorActionPreference = 'Stop'

# 스크립트 위치 기준 manifests 디렉토리 절대 경로 계산
$manifestDir = (Resolve-Path "$PSScriptRoot\..\manifests").Path

Write-Host "=== K8s 매니페스트 적용 ===" -ForegroundColor Cyan
Write-Host "매니페스트 디렉토리: $manifestDir"
Write-Host ""

# [주의] deployment.yaml 의 <POSTGRES_PASSWORD> 가 치환되지 않으면
# 파드가 DB 연결 실패로 CrashLoopBackOff 에 빠짐
Write-Host "[확인] deployment.yaml 에서 <POSTGRES_PASSWORD> 를 치환했는지 반드시 확인하세요" -ForegroundColor Yellow
Write-Host ""

# 세 매니페스트 동시 적용 (순서: Deployment → Service → HPA)
kubectl apply `
  -f "$manifestDir\deployment.yaml" `
  -f "$manifestDir\service.yaml" `
  -f "$manifestDir\hpa.yaml"

Write-Host ""
Write-Host "=== Deployment 준비 대기 (최대 120초) ===" -ForegroundColor Cyan

# 파드가 Ready 상태가 될 때까지 대기
# readinessProbe 설정에 따라 /health 응답 성공 후 완료
kubectl wait --for=condition=available --timeout=120s deployment/framework-api

Write-Host ""
Write-Host "=== 배포 상태 확인 ===" -ForegroundColor Cyan
kubectl get pods,svc,hpa

Write-Host ""
Write-Host "[완료] 배포 성공. 다음: 05-verify.ps1" -ForegroundColor Green

