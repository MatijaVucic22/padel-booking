$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$localScript = Join-Path $PSScriptRoot "dev-no-docker.ps1"
$cloudflaredCommand = Get-Command cloudflared -ErrorAction SilentlyContinue

if (-not $cloudflaredCommand) {
    throw "cloudflared nije dostupan. Instalirajte Cloudflare cloudflared i proverite PATH."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK nije dostupan. Instalirajte .NET 10 SDK i proverite PATH."
}
if (-not (Get-Command node -ErrorAction SilentlyContinue) -or
    -not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw "Node.js/npm nije dostupan. Instalirajte Node.js i proverite PATH."
}

function Test-PortInUse {
    param([int]$Port)

    $ipProperties = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    return $ipProperties.GetActiveTcpListeners().Port -contains $Port
}

foreach ($port in @(5173, 5238)) {
    if (Test-PortInUse -Port $port) {
        throw "Port $port je vec zauzet. Zaustavite postojeci frontend/backend ili Docker stack."
    }
}

function Stop-StartedProcessTree {
    param([System.Diagnostics.Process]$Process)

    if ($Process -and -not $Process.HasExited) {
        & taskkill.exe /PID $Process.Id /T /F 2>$null | Out-Null
    }
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "padelbooking-mobile-$([Guid]::NewGuid().ToString('N'))"
$stdoutPath = Join-Path $temporaryDirectory "cloudflared.out.log"
$stderrPath = Join-Path $temporaryDirectory "cloudflared.err.log"
$cloudflaredProcess = $null

New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
Set-Location -LiteralPath $repoRoot

try {
    Write-Host "Kreiranje Cloudflare Quick Tunnel-a..."
    $cloudflaredProcess = Start-Process -FilePath $cloudflaredCommand.Source -ArgumentList @(
        "tunnel",
        "--url", "http://[::1]:5173",
        "--http-host-header", "localhost:5173",
        "--loglevel", "info"
    ) -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow -PassThru

    $mobileUrl = $null
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        if ($cloudflaredProcess.HasExited) {
            throw "Cloudflare Quick Tunnel je zavrsen pre nego sto je URL generisan."
        }

        $output = @()
        if (Test-Path -LiteralPath $stdoutPath) { $output += Get-Content -LiteralPath $stdoutPath -Raw }
        if (Test-Path -LiteralPath $stderrPath) { $output += Get-Content -LiteralPath $stderrPath -Raw }
        $match = [regex]::Match(($output -join "`n"), 'https://[a-z0-9-]+\.trycloudflare\.com')
        if ($match.Success) {
            $mobileUrl = $match.Value.TrimEnd('/')
            break
        }

        Start-Sleep -Milliseconds 250
    }

    if (-not $mobileUrl) {
        throw "Cloudflare Quick Tunnel URL nije dobijen u roku od 30 sekundi."
    }

    $mobileUri = [Uri]$mobileUrl
    $env:Cors__AdditionalOrigin = $mobileUrl
    $env:STRIPE_FRONTEND_URL = $mobileUrl
    $env:__VITE_ADDITIONAL_SERVER_ALLOWED_HOSTS = $mobileUri.Host
    $env:PADELBOOKING_MOBILE_URL = $mobileUrl

    & $localScript
}
finally {
    Stop-StartedProcessTree -Process $cloudflaredProcess
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
