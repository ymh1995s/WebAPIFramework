# 05-verify.ps1
# 클러스터 배포 상태 및 엔드포인트 동작 검증
# k6 시나리오 실행 전 최종 확인 용도

$ErrorActionPreference = 'Stop'

Write-Host "=== 파드/서비스/HPA 상태 ===" -ForegroundColor Cyan
# 파드 STATUS=Running, READY=1/1 확인
# HPA CPU 수치 확인 (metrics-server 설치 직후라면 <unknown> 일 수 있음 — 대기 필요)
kubectl get pods,svc,hpa

Write-Host ""
Write-Host "=== ping 테스트 (NodePort 30080) ===" -ForegroundColor Cyan
# 시나리오 3(CPU 집약) 핵심 엔드포인트
# 예상 응답: {"message":"pong"} 또는 유사
curl http://localhost:30080/api/load-test/ping

Write-Host ""
Write-Host "=== ranking/me 테스트 (DB 시나리오용) ===" -ForegroundColor Cyan
# 시나리오 4(DB 집약) 핵심 엔드포인트
# 인증 우회(LoadTest 빌드)로 PlayerId=1 고정 응답 예상
curl http://localhost:30080/api/ranking/me

Write-Host ""
Write-Host "=== 파드별 CPU/메모리 사용량 ===" -ForegroundColor Cyan
# kubectl top 은 metrics-server 가 메트릭 수집한 이후(30~60초)에 동작
# 'error: Metrics API not available' 가 뜨면 03-install-metrics-server.ps1 재확인
kubectl top pods

Write-Host ""
Write-Host "[완료] 검증 완료. k6 시나리오를 실행할 준비가 됐습니다." -ForegroundColor Green
Write-Host "k6 실행 방법은 k8s/README.md 의 '시나리오 3/4 측정 방법' 섹션을 참조하세요." -ForegroundColor Yellow

