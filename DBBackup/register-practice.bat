@echo off
REM Practice only - register backup-practice.ps1 (same folder) as a
REM scheduled task: every 1 min for 30 min (auto-expires).
REM %~dp0 = this folder (DBBackup). No admin needed.
REM ASCII-only: cmd parses .bat in OEM codepage; UTF-8 Korean + chcp
REM corrupts the parser. Keep batch files ASCII. (See SERVER_GUIDE)
powershell -ExecutionPolicy Bypass -Command "Register-ScheduledTask -TaskName 'PG_Backup_Practice' -Force -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NonInteractive -ExecutionPolicy Bypass -File \"%~dp0backup-practice.ps1\"') -Trigger (New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Minutes 30))"
echo Registered (PG_Backup_Practice, 1min/30min). Remove: schtasks /delete /tn PG_Backup_Practice /f
pause
