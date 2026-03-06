@echo off
title Courier E2E Tests (UI Mode)
cd /d "%~dp0"

echo Discovering Aspire service ports...
for /f "tokens=1,2 delims==" %%a in ('powershell -NoProfile -ExecutionPolicy Bypass -File "e2e\discover-ports.ps1"') do (
    set "%%a=%%b"
)

if not defined API_URL (
    echo ERROR: Could not find Courier API. Is the Aspire stack running?
    pause
    exit /b 1
)
if not defined FRONTEND_URL (
    echo ERROR: Could not find Courier Frontend. Is the Aspire stack running?
    pause
    exit /b 1
)

echo API:      %API_URL%
echo Frontend: %FRONTEND_URL%
echo.
echo Opening Playwright UI...
echo.
npx playwright test --ui
