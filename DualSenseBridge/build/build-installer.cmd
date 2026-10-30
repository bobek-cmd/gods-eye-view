@echo off
rem Double-click friendly wrapper around build-installer.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" %*
if errorlevel 1 pause
