@echo off
chcp 65001 >nul
title FaceIT CS2 Challenger ^& Banned Checker
cd /d "%~dp0"

echo =================================================================
echo   Khoi dong FaceIT CS2 Challenger Checker...
echo =================================================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0test_faceit.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Co loi xay ra khi chay script PowerShell.
)

pause
