@echo off
rem Double-click to install SaltMap. Extra arguments go to install.ps1, e.g.  Install.cmd -SsMap "D:\SSMap"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
pause
