@echo off
setlocal
cd /d "%~dp0"
for %%I in ("%~dp0..") do set "HP2_DATA=%%~fI"
if not exist "%~dp0hp2.ini" if exist "%HP2_DATA%\hp2.ini" copy /y "%HP2_DATA%\hp2.ini" "%~dp0hp2.ini" >nul
set "HP2_INI=%~dp0hp2.ini"
set "HP2_CLOUDS=1"
set "HP2_CLOUD_STEPS=48"
set "HP2_CLOUD_PROFILE="
set "HP2_MEMCARD="
set "HP2_DEMOREPLAY="
set "HP2_QUICKDEMO="
set "HP2_KEYSCRIPT="
set "HP2_NOAUDIO="
set "HP2_NATIVESHOT="
set "HP2_SHOTSTEPS="
start "" "%~dp0speed-clouds-lapse-4x.exe"
