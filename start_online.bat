@echo off
rem One click for a live demo: tunnel + server (public mode, house table). See KittyAPI\docs\DEPLOY.md.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start_online.ps1"
if errorlevel 1 pause
