@echo off
setlocal
title Catino v2 server (port 8001)

rem Double-click to start the Catino v2 server on port 8001 using the "hackathon" conda env.
rem Uses KittyAPI\data\catino-v2.db (created and migrated on start) unless KITTY_DB_PATH is set.
rem The v1 demo keeps port 8000 and data\kittycat.db; this server never opens that file.
rem Settings and local secrets can go in KittyAPI\.env.local (KEY=value lines, never committed):
rem DEMO_MODE=1 (simulated purchases), TICKET_SECRET, QR_SECRET, HAPPY_HOUR=1 (demo season bonuses).

set "KITTY_API=%~dp0KittyAPI"
set "ENV_NAME=hackathon"
set "PORT=8001"

rem --- find conda ---
set "CONDA_ROOT="
if defined CONDA_EXE for %%I in ("%CONDA_EXE%\..\..") do set "CONDA_ROOT=%%~fI"
if not defined CONDA_ROOT if exist "D:\Anaconda\condabin\conda.bat" set "CONDA_ROOT=D:\Anaconda"
if not defined CONDA_ROOT if exist "%USERPROFILE%\anaconda3\condabin\conda.bat" set "CONDA_ROOT=%USERPROFILE%\anaconda3"
if not defined CONDA_ROOT if exist "%USERPROFILE%\miniconda3\condabin\conda.bat" set "CONDA_ROOT=%USERPROFILE%\miniconda3"

if not defined CONDA_ROOT (
    echo.
    echo  [X] Conda was not found.
    echo      Install Anaconda or Miniconda ^(expected at D:\Anaconda^), then run:
    echo        conda env create -f "%KITTY_API%\environment.yml"
    echo.
    pause
    exit /b 1
)

if not exist "%CONDA_ROOT%\envs\%ENV_NAME%\python.exe" (
    echo.
    echo  [X] Conda found at %CONDA_ROOT%, but the "%ENV_NAME%" environment is missing.
    echo      Create it once with:
    echo        "%CONDA_ROOT%\condabin\conda.bat" env create -f "%KITTY_API%\environment.yml"
    echo.
    pause
    exit /b 1
)

rem --- activate (puts the env's DLL folders on PATH so numpy/qiskit load) ---
call "%CONDA_ROOT%\condabin\conda.bat" activate %ENV_NAME%
if errorlevel 1 (
    echo  [X] Could not activate the "%ENV_NAME%" environment.
    pause
    exit /b 1
)

netstat -ano | findstr /r /c:":%PORT% .*LISTENING" >nul
if not errorlevel 1 (
    echo.
    echo  [X] Port %PORT% is already in use. Is the server already running in another window?
    echo.
    pause
    exit /b 1
)

cd /d "%KITTY_API%"
echo.
echo  Catino v2 server starting on port %PORT%
if defined KITTY_DB_PATH (echo  Database: %KITTY_DB_PATH%) else (echo  Database: %KITTY_API%\data\catino-v2.db)
if "%DEMO_MODE%"=="1" echo  DEMO MODE: payments are simulated
if exist "%KITTY_API%\.env.local" echo  Local settings: KittyAPI\.env.local
echo  Game:   http://127.0.0.1:%PORT%      Phones: http://^<this PC's Wi-Fi IP^>:%PORT%/crowd
echo  Close this window or press Ctrl+C to stop.
echo.

python -m uvicorn main:app --host 0.0.0.0 --port %PORT%

echo.
echo  Server stopped.
pause
