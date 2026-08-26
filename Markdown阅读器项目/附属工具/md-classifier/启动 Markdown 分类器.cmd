@echo off
title Markdown 文件分类器
set "LOG_FILE=%~dp0启动错误.log"
del "%LOG_FILE%" 2>nul
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0md-classifier.ps1" 1>"%LOG_FILE%" 2>&1
echo.
if exist "%LOG_FILE%" type "%LOG_FILE%"
echo.
echo 阅读器窗口关闭后，按任意键退出此窗口。
pause
