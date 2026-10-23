@echo off
setlocal
title Catino server for the internet (port 8001)

rem Starts the v2 server for a tunnel or a public host: PUBLIC_MODE=1, listening on this computer only,
rem so just the tunnel can reach it. The cashier and operator pages are closed in this mode.
rem 1. Run start_tunnel.bat first and copy the https address it prints.
rem 2. Run this file and paste that address when asked (it goes into the join QR code).
rem The Unity game on this computer still hosts the table (Use Api, server http://127.0.0.1:8001).

set "KITTY_API=%~dp0KittyAPI"
set "ENV_NAME=hackathon"
set "PORT=8001"

set "CONDA_ROOT="
if defined CONDA_EXE for %%I in ("%CONDA_EXE%\..\..") do set "CONDA_ROOT=%%~fI"
if not defined CONDA_ROOT if exist "D:\Anaconda\condabin\conda.bat" set "CONDA_ROOT=D:\Anaconda"
if not defined CONDA_ROOT if exist "%USERPROFILE%\anaconda3\condabin\conda.bat" set "CONDA_ROOT=%USERPROFILE%\anaconda3"
if not defined CONDA_ROOT if exist "%USERPROFILE%\miniconda3\condabin\conda.bat" set "CONDA_ROOT=%USERPROFILE%\miniconda3"
if not defined CONDA_ROOT (
    echo  [X] Conda was not found. See start_server.bat for how to set it up.
    pause
    exit /b 1
)
if not exist "%CONDA_ROOT%\envs\%ENV_NAME%\python.exe" (
    echo  [X] The "%ENV_NAME%" environment is missing. See start_server.bat.
    pause
    exit /b 1
)
call "%CONDA_ROOT%\condabin\conda.bat" activate %ENV_NAME%
if errorlevel 1 (
    echo  [X] Could not activate the "%ENV_NAME%" environment.
    pause
    exit /b 1
)

netstat -ano | findstr /r /c:":%PORT% .*LISTENING" >nul
if not errorlevel 1 (
    echo  [X] Port %PORT% is already in use. Close the other server window first.
    pause
    exit /b 1
)

if not defined PUBLIC_URL set /p PUBLIC_URL=Paste the public https address (or press Enter to skip):

set "PUBLIC_MODE=1"
rem Simulated top-ups (card / wallet checkout, clearly labelled, no real money) stay on for the demo.
set "DEMO_MODE=1"
if not defined ALLOW_PUBLIC_DEMO set "ALLOW_PUBLIC_DEMO=1"
if not defined AUTO_HOST set "AUTO_HOST=1"

cd /d "%KITTY_API%"
echo.
echo  Catino server (public mode) on 127.0.0.1:%PORT%
if defined PUBLIC_URL (echo  Public address: %PUBLIC_URL%/crowd) else (echo  No public address set: the join QR will show the Wi-Fi address)
echo  House table is on: phones can play without the Unity game. Web game: /play when a build exists.
echo  Cashier and operator pages are closed. Close this window or press Ctrl+C to stop.
echo.

python -m uvicorn main:app --host 127.0.0.1 --port %PORT% --proxy-headers --forwarded-allow-ips=127.0.0.1

echo.
echo  Server stopped.
pause
