# One click: a Cloudflare quick tunnel plus the Catino server in public mode (house table on).
# Prints the public address; phones open <address>/crowd and the web game is at <address>/play (after a WebGL build).
# If the Vercel command line is set up on this PC, the landing page (catino.vercel.app) is also updated so its
# "Play" buttons point at this run, and switched back to "offline" when you stop.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Publish-Link([string]$root, [string]$address) {
    $vercel = Join-Path $root ".tmp\vercel\node_modules\.bin\vercel.cmd"
    $site = Join-Path $root "site"
    if (-not (Test-Path $vercel) -or -not (Test-Path (Join-Path $site ".vercel"))) { return }
    $tmp = Join-Path $root ".tmp\site-deploy"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Copy-Item $site $tmp -Recurse
    Set-Content -Path (Join-Path $tmp "live.json") -Value ('{"live": "' + $address + '"}') -Encoding ascii
    Push-Location $tmp
    try { & $vercel deploy --prod --yes 2>&1 | Out-Null } catch { } finally { Pop-Location }
}

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
Write-Host "  Web game:       $url/play/"
Write-Host "  Landing page:   https://catino.vercel.app  (updated in the background)"
Write-Host ""
$env:PUBLIC_URL = $url
$job = Start-Job -ScriptBlock ${function:Publish-Link} -ArgumentList $root, $url
try { & (Join-Path $root "start_public.bat") } finally {
    Stop-Process -Id $tunnel.Id -Force -ErrorAction SilentlyContinue
    Write-Host "  Switching the landing page to offline..."
    Receive-Job $job -Wait -AutoRemoveJob -ErrorAction SilentlyContinue | Out-Null
    Publish-Link $root ""
}
