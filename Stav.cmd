@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0NoDOF.ps1" -Action Status
pause
