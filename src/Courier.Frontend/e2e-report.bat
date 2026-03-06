@echo off
title Courier E2E Test Report
cd /d "%~dp0"
echo Opening Playwright HTML report...
echo.
npx playwright show-report
