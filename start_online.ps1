# One click: a Cloudflare quick tunnel plus the Catino server in public mode (house table on).
# Prints the public address; phones open <address>/crowd and the web game is at <address>/play (after a WebGL build).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cf = (Get-Command cloudflared -ErrorAction SilentlyContinue).Source
if (-not $cf) { $cf = Join-Path $root ".tmp\cloudflared\cloudflared.exe" }
if (-not (Test-Path $cf)) {
    Write-Host "Downloading cloudflared (once, no account needed)..."
    New-Item -ItemType Directory -Force (Split-Path $cf) | Out-Null
    Invoke-WebRequest "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile $cf
}
$log = Join-Path $root ".tmp\online\tunnel.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
Remove-Item $log -ErrorAction SilentlyContinue
$tunnel = Start-Process -FilePath $cf -ArgumentList "tunnel", "--url", "http://127.0.0.1:8001", "--no-autoupdate", "--logfile", $log -WindowStyle Hidden -PassThru
$url = $null
for ($i = 0; $i -lt 45 -and -not $url; $i++) {
    Start-Sleep 1
    if (Test-Path $log) {
        $m = Select-String -Path $log -Pattern "https://[a-z0-9-]+\.trycloudflare\.com" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($m) { $url = $m.Matches[0].Value }
    }
}
if (-not $url) { Stop-Process -Id $tunnel.Id -Force -ErrorAction SilentlyContinue; throw "The tunnel did not start. Is this computer online?" }
Write-Host ""
Write-Host "  Public address: $url" -ForegroundColor Green
Write-Host "  Phones:         $url/crowd"
Write-Host "  Web game:       $url/play"
Write-Host ""
$env:PUBLIC_URL = $url
try { & (Join-Path $root "start_public.bat") } finally { Stop-Process -Id $tunnel.Id -Force -ErrorAction SilentlyContinue }
