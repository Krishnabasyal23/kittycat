@echo off
setlocal
title Catino tunnel

rem Gives the local server (port 8001) a public https address. Needs ONE of these installed:
rem   cloudflared  (Cloudflare Tunnel, no account needed for a quick tunnel)
rem   ngrok        (free account + one-time "ngrok config add-authtoken ...")
rem Start the server with start_public.bat, then paste the https address printed here into it.
rem Leave this window open while people play.

where cloudflared >nul 2>nul
if not errorlevel 1 (
    echo  Using cloudflared. Look for the https://....trycloudflare.com address below.
    echo.
    cloudflared tunnel --url http://127.0.0.1:8001
    goto done
)
where ngrok >nul 2>nul
if not errorlevel 1 (
    echo  Using ngrok. Look for the "Forwarding" https address below.
    echo.
    ngrok http 127.0.0.1:8001
    goto done
)
echo  [X] Neither cloudflared nor ngrok was found on PATH.
echo      Install one of them (see KittyAPI\docs\DEPLOY.md) and run this again.
:done
echo.
pause
