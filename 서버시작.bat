@echo off
REM =============================================================
REM  Full server bring-up (single entry point) - fully containerized
REM  Docker Desktop -> docker compose up -d (db/api/caddy/ELK)
REM                 -> register daily DB backup task
REM
REM  Prereq: .env must exist in this folder (secrets, user-authored).
REM          Public DNS api.overture.io.kr -> this host + 80/443 open
REM          enables auto HTTPS. First run builds api image (slow).
REM          DB schema is auto-migrated by the api container on boot.
REM  K8s distributed track is separate: k8s\scripts\01~05
REM  (Korean notes intentionally omitted - cmd parses .bat in OEM
REM   codepage; UTF-8 Korean + chcp breaks the parser. See SERVER_GUIDE.)
REM =============================================================

REM -- Admin rights required for Register-ScheduledTask -RunLevel Highest
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Run as Administrator.
    pause
    exit /b 1
)

REM -- .env guard (missing -> compose secrets blank -> DB/JWT broken)
if not exist "%~dp0.env" (
    echo [ERROR] .env not found. Copy .env.example and fill secrets.
    pause
    exit /b 1
)

echo [1/3] Starting Docker Desktop...
start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
timeout /t 15 /nobreak

echo [2/3] Bringing up full stack (db/api/caddy/ELK)...
docker compose -f "%~dp0docker-compose.yml" up -d --build

echo [3/3] Registering daily backup task...
powershell -Command "Register-ScheduledTask -TaskName 'PostgreSQL_Daily_Backup' -Force -RunLevel Highest -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NonInteractive -ExecutionPolicy Bypass -File \"%~dp0DBBackup\backup-daily.ps1\"') -Trigger (New-ScheduledTaskTrigger -Daily -At '03:00AM') -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 1) -StartWhenAvailable)"

echo.
echo Done. Check: docker compose ps
echo  - API (HTTPS) : https://api.overture.io.kr  (if DNS/ports ready)
echo  - Backup      : daily 03:00, stored in OneDrive\DB_Backup
pause
