# ─────────────────────────────────────────────────────────────
# PostgreSQL 백업 스크립트 — 실습용 (1분 주기 / 30분 보존)
#
# [위치] 저장소 DBBackup 폴더에서 그대로 실행. 이동 불필요.
#        register-practice.bat 이 같은 폴더(%~dp0) 기준으로 호출.
#
# [전제] 작업 스케줄러가 1분마다 호출. 1회 실행 = 덤프 1건 + 정리 1회.
#        반복은 스케줄러가 담당하며 이 스크립트에 루프는 없다.
#
# [형식] pg_dump -Fc (커스텀 압축, 바이너리). 복원은 pg_restore 사용.
# ─────────────────────────────────────────────────────────────

# [네이밍 주의] 이름 변경 시 3곳 동시 수정 필수:
#   docker-compose.yml container_name / backup-daily.ps1 / backup-practice.ps1
# 한 곳만 바꾸면 docker exec 대상 불일치로 백업이 조용히 실패한다.
$ContainerName = "framework-postgres"   # 실행 중인 PostgreSQL 컨테이너
$DbUser        = "postgres"             # 로컬 ad-hoc 컨테이너 롤 (실습 전용. 배포본은 backup-daily.ps1=framework_user)
$DbName        = "framework_db"         # 백업 대상 DB
$BackupDir     = "C:\Users\user\OneDrive\DB_Backup"  # 로컬+OneDrive 동기화 폴더
$RetainMinutes = 30                     # 실습 보존주기: 30분 초과분 삭제

# 백업 폴더 보장 (없으면 생성)
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }

# 타임스탬프 파일명 (예: framework_db_20260517_153000.dump)
$FileName = "${DbName}_$(Get-Date -Format 'yyyyMMdd_HHmmss').dump"
$FilePath = Join-Path $BackupDir $FileName

# -Fc 는 바이너리라 PowerShell 파이프 금지 → 컨테이너 내부 덤프 후 docker cp
docker exec $ContainerName pg_dump -U $DbUser -Fc -f /tmp/backup.dump $DbName
if ($LASTEXITCODE -ne 0) {
    Write-Host "백업 실패 (pg_dump 오류)" -ForegroundColor Red
    exit 1
}

# 호스트로 복사 후 컨테이너 임시파일 제거
docker cp "${ContainerName}:/tmp/backup.dump" $FilePath
if ($LASTEXITCODE -ne 0) {
    Write-Host "백업 실패 (docker cp 오류)" -ForegroundColor Red
    exit 1
}
docker exec $ContainerName rm /tmp/backup.dump
Write-Host "백업 완료: $FilePath"

# 보존주기 초과분 삭제 (실습=분 단위. 운영본은 backup-daily.ps1 참조)
Get-ChildItem -Path $BackupDir -Filter "*.dump" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddMinutes(-$RetainMinutes) } |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "오래된 백업 삭제: $($_.Name)"
    }
