# 99-cleanup.ps1
# 부하테스트 완료 후 kind 클러스터 제거 스크립트
# 클러스터 삭제 시 배포된 모든 리소스(파드, 서비스, HPA)가 함께 제거됨

$ErrorActionPreference = 'Stop'

# kind 클러스터 이름 — kind-config.yaml name 필드와 일치
$clusterName = "framework-loadtest"

Write-Host "=== kind 클러스터 제거 ===" -ForegroundColor Cyan
Write-Host "클러스터: $clusterName"

# 클러스터 삭제 — kind 노드 컨테이너 및 관련 네트워크 모두 제거
kind delete cluster --name $clusterName

Write-Host ""
Write-Host "[완료] 클러스터 제거 완료." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# [선택] 아래 주석을 해제하면 빌드한 Docker 이미지도 함께 제거
# 이미지 크기가 크거나 디스크 정리가 필요한 경우에만 사용
# ─────────────────────────────────────────────────────────────────────────────
# docker rmi framework-api:loadtest

