# ─────────────────────────────────────────────────────────────
# PostgreSQL 백업 스크립트 — 운영용 (매일 1회 / 30일 보존)
#
# [위치] 저장소 DBBackup 폴더에서 그대로 실행. 이동 불필요.
#        서버시작-local.bat / 서버시작-k8s.bat 이 상대경로로 등록한다.
#        (백업 *출력물*만 OneDrive 폴더로 — 로컬+클라우드 2중 보호.
#         스크립트 자체는 저장소에 둔다.)
#
# [전제] 작업 스케줄러가 매일 03:00 1회 호출.
#        1회 실행 = 덤프 1건 + 30일 초과분 정리.
#
# [형식] pg_dump -Fc (커스텀 압축, 바이너리). 복원은 pg_restore 사용.
# ─────────────────────────────────────────────────────────────

# [네이밍 주의] 이름 변경 시 3곳 동시 수정 필수:
#   docker-compose.yml container_name / backup-daily.ps1 / backup-practice.ps1
# 한 곳만 바꾸면 docker exec 대상 불일치로 백업이 조용히 실패한다.
$ContainerName = "framework-postgres"   # 운영 PostgreSQL 컨테이너
$DbUser        = "framework_user"       # 로그인 롤 (compose POSTGRES_USER 와 일치)
$DbName        = "framework_db"         # 백업 대상 DB
$BackupDir     = "C:\Users\user\OneDrive\DB_Backup"  # 로컬+OneDrive 동기화 폴더
$RetainDays    = 30                     # 운영 보존주기: 30일 초과분 삭제

# 백업 폴더 보장 (없으면 생성)
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }

# 타임스탬프 파일명 (예: framework_db_20260517_030000.dump)
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

# 30일 초과 백업본 삭제 (운영 정책: 1일 1회 / 30일 보관)
Get-ChildItem -Path $BackupDir -Filter "*.dump" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetainDays) } |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "오래된 백업 삭제: $($_.Name)"
    }
