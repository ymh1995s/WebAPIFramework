# 02-build-image.ps1
# Framework.Api LoadTest 빌드 구성 Docker 이미지 빌드 및 kind 클러스터 로드
# LoadTest 빌드 구성 = 인증 우회 (#if DEBUG 블록) 활성화
# 외부 레지스트리 push 없이 kind load 로 클러스터 내부에 직접 주입

$ErrorActionPreference = 'Stop'

# Framework/ 루트 디렉토리 (빌드 컨텍스트)
# Dockerfile 내 COPY 경로가 Framework/ 기준이므로 이 경로가 빌드 컨텍스트여야 함
$frameworkRoot = (Resolve-Path "$PSScriptRoot\..\..\Framework").Path

# Dockerfile.loadtest 경로 — k8s 폴더는 WebAPIFramework/k8s (Framework 폴더 밖)
$dockerfile = (Resolve-Path "$PSScriptRoot\..\docker\Dockerfile.loadtest").Path

# 이미지 태그 — deployment.yaml image 필드와 반드시 일치
$imageTag = "framework-api:loadtest"

# kind 클러스터 이름 — kind-config.yaml name 필드와 일치
$clusterName = "framework-loadtest"

Write-Host "=== Docker 이미지 빌드 ===" -ForegroundColor Cyan
Write-Host "컨텍스트: $frameworkRoot"
Write-Host "Dockerfile: $dockerfile"
Write-Host "태그: $imageTag"

# 빌드 실행 (-f 로 Dockerfile 경로 명시, 마지막 인자가 빌드 컨텍스트 디렉토리)
docker build -t $imageTag -f $dockerfile $frameworkRoot

Write-Host ""
Write-Host "=== kind 클러스터로 이미지 로드 ===" -ForegroundColor Cyan
Write-Host "클러스터: $clusterName"

# kind load: 외부 레지스트리 없이 로컬 Docker 이미지를 kind 노드에 직접 복사
# deployment.yaml imagePullPolicy=IfNotPresent 이므로 이 단계 필수
kind load docker-image $imageTag --name $clusterName

Write-Host ""
Write-Host "=== 로컬 이미지 확인 ===" -ForegroundColor Cyan
# framework-api 이미지가 목록에 보이는지 검증
docker images | Select-String "framework-api"

Write-Host ""
Write-Host "[완료] 이미지 빌드 및 로드 성공. 다음: 03-install-metrics-server.ps1" -ForegroundColor Green

