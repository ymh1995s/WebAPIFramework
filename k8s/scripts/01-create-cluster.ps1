# 01-create-cluster.ps1
# 부하테스트용 kind 클러스터 생성 스크립트
# 실행 전 kind, kubectl 이 설치되어 있어야 함
# 설치: winget install Kubernetes.kind / winget install Kubernetes.kubectl

$ErrorActionPreference = 'Stop'

# 스크립트 위치 기준으로 kind-config.yaml 절대 경로 계산
# $PSScriptRoot = 이 파일이 있는 디렉토리 (k8s/scripts/)
$configPath = (Resolve-Path "$PSScriptRoot\..\kind-config.yaml").Path

Write-Host "=== kind 클러스터 생성 ===" -ForegroundColor Cyan
Write-Host "설정 파일: $configPath"

# kind 클러스터 생성 (control-plane 1 + worker 3)
# 이미 동일 이름 클러스터가 존재하면 오류 발생 — 99-cleanup.ps1 로 삭제 후 재실행할 것
kind create cluster --config $configPath

Write-Host ""
Write-Host "=== 클러스터 정보 검증 ===" -ForegroundColor Cyan

# kubectl context 가 kind-framework-loadtest 로 전환되었는지 확인
kubectl cluster-info --context kind-framework-loadtest

Write-Host ""
Write-Host "=== 노드 상태 확인 (Ready 될 때까지 30초 내외 소요) ===" -ForegroundColor Cyan
kubectl get nodes

Write-Host ""
Write-Host "[완료] 클러스터 생성 성공. 다음: 02-build-image.ps1" -ForegroundColor Green

