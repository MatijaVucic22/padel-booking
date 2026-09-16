$ErrorActionPreference = "Stop"

$frontendRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repoRoot = (Resolve-Path (Join-Path $frontendRoot "..")).Path
$composeFile = Join-Path $repoRoot "docker-compose.e2e.yml"
$project = "padelbooking-e2e-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$viteProcess = $null
$composeStarted = $false
$exitCode = 1
$isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)
$npmCommand = if ($isWindowsHost) { "npm.cmd" } else { "npm" }
$npxCommand = if ($isWindowsHost) { "npx.cmd" } else { "npx" }

function Assert-ExitCode([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "$step nije uspeo (exit code $LASTEXITCODE)." }
}

function Wait-ForHttp([string]$url, [int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return }
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "Servis nije spreman: $url"
}

try {
    foreach ($port in @(5189, 5248)) {
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $client.Connect("127.0.0.1", $port)
            throw "Port $port je zauzet; zaustavi proces koji ga koristi pre E2E testa."
        } catch [System.Net.Sockets.SocketException] {
            # Connection refused means the port is available.
        } finally {
            $client.Dispose()
        }
    }

    $env:E2E_MYSQL_PASSWORD = [Guid]::NewGuid().ToString("N")
    $env:E2E_MYSQL_ROOT_PASSWORD = [Guid]::NewGuid().ToString("N")
    $env:E2E_JWT_KEY = [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
    $env:PADELBOOKING_E2E_ISOLATED = "1"
    $env:PADELBOOKING_E2E_API_URL = "http://localhost:5248/api"
    $env:PADELBOOKING_E2E_REUSE_TEST_SERVER = "1"
    $env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $frontendRoot ".playwright-browsers"

    Push-Location $repoRoot
    try {
        Write-Host "Pokrecem izolovani MySQL i backend..."
        $composeStarted = $true
        & docker compose -p $project -f $composeFile up -d --build
        Assert-ExitCode "Docker Compose"
        Wait-ForHttp "http://localhost:5248/api/courts" 120

        Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot "seed.sql") |
            & docker compose -p $project -f $composeFile exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_PASSWORD" exec mysql -u "$MYSQL_USER" "$MYSQL_DATABASE"'
        Assert-ExitCode "E2E court seed"
    } finally {
        Pop-Location
    }

    $env:VITE_API_URL = $env:PADELBOOKING_E2E_API_URL
    $viteArguments = @{
        FilePath = (Get-Command node).Source
        ArgumentList = @("node_modules/vite/bin/vite.js", "--host", "127.0.0.1", "--port", "5189", "--strictPort")
        WorkingDirectory = $frontendRoot
        PassThru = $true
    }
    if ($isWindowsHost) { $viteArguments.WindowStyle = "Hidden" }
    $viteProcess = Start-Process @viteArguments
    Wait-ForHttp "http://localhost:5189" 30

    $browserPath = Join-Path $env:PLAYWRIGHT_BROWSERS_PATH "chromium_headless_shell-*"
    if (-not (Get-ChildItem $browserPath -ErrorAction SilentlyContinue)) {
        Push-Location $frontendRoot
        try {
            Write-Host "Instaliram Playwright Chromium..."
            & $npxCommand playwright install chromium
            Assert-ExitCode "Chromium instalacija"
        } finally {
            Pop-Location
        }
    }

    Push-Location $frontendRoot
    try {
        & $npmCommand run test:e2e
        Assert-ExitCode "Playwright testovi"
    } finally {
        Pop-Location
    }
    $exitCode = 0
} catch {
    Write-Host "E2E nije uspeo: $($_.Exception.Message)"
} finally {
    if ($viteProcess -and -not $viteProcess.HasExited) {
        Stop-Process -Id $viteProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($composeStarted) {
        Push-Location $repoRoot
        try {
            & docker compose -p $project -f $composeFile down --volumes --rmi local --remove-orphans
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Upozorenje: proveri E2E Docker cleanup za projekat $project."
                $exitCode = 1
            }
        } finally {
            Pop-Location
        }
    }
    Remove-Item Env:E2E_MYSQL_PASSWORD,Env:E2E_MYSQL_ROOT_PASSWORD,Env:E2E_JWT_KEY -ErrorAction SilentlyContinue
}

exit $exitCode
