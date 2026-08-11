@echo off
setlocal
cd /d "%~dp0"
title MGSV DDSFileTypePlus - VS2026 Build

echo Starting PowerShell build helper...
echo This window will NOT relaunch itself or disappear.
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -NoExit -File "%~dp0BUILD_MGSV_VS2026.ps1"
