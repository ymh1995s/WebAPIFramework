# 03-install-metrics-server.ps1
# HPA 가 CPU 메트릭을 읽으려면 metrics-server 가 필요
# kind 환경은 kubelets 가 자체 서명 TLS를 사용하므로 --kubelet-insecure-tls 패치 필요

$ErrorActionPreference = 'Stop'

Write-Host "=== metrics-server 설치 ===" -ForegroundColor Cyan

# 공식 최신 릴리스 매니페스트 적용
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

Write-Host ""
Write-Host "=== kind 환경 TLS 우회 패치 적용 ===" -ForegroundColor Cyan

# kind 노드의 kubelet은 자체 서명 인증서를 사용
# --kubelet-insecure-tls 없으면 metrics-server 가 메트릭 수집 실패 → HPA <unknown>
kubectl patch deployment metrics-server -n kube-system --type='json' `
  -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"}]'

Write-Host ""
Write-Host "=== metrics-server 준비 대기 (최대 120초) ===" -ForegroundColor Cyan

# Deployment 가 Available 상태가 될 때까지 대기
kubectl wait --for=condition=available --timeout=120s deployment/metrics-server -n kube-system

Write-Host ""
Write-Host "[완료] metrics-server 설치 성공. 다음: 04-deploy.ps1" -ForegroundColor Green
Write-Host "[참고] HPA 에서 CPU 수치가 <unknown>으로 보이면 30~60초 더 대기 후 kubectl get hpa 재확인" -ForegroundColor Yellow

