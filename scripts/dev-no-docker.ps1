$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot ".env"

if (-not (Test-Path -LiteralPath $envPath)) {
    throw "Nedostaje root .env fajl. Kopirajte .env.example u .env i unesite lokalne vrednosti."
}

foreach ($line in Get-Content -LiteralPath $envPath) {
    $trimmed = $line.Trim()
    if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }

    $separatorIndex = $trimmed.IndexOf("=")
    if ($separatorIndex -lt 1) { continue }

    $name = $trimmed.Substring(0, $separatorIndex).Trim()
    $value = $trimmed.Substring($separatorIndex + 1).Trim()
    if ($value.Length -ge 2 -and
        (($value.StartsWith('"') -and $value.EndsWith('"')) -or
         ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    if ($null -eq [Environment]::GetEnvironmentVariable($name, "Process")) {
        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

$requiredVariables = @(
    "MYSQL_PASSWORD",
    "JWT_KEY",
    "STRIPE_SECRET_KEY",
    "STRIPE_WEBHOOK_SECRET"
)

$missingVariables = $requiredVariables | Where-Object {
    $value = [Environment]::GetEnvironmentVariable($_, "Process")
    [string]::IsNullOrWhiteSpace($value)
}

if ($missingVariables.Count -gt 0) {
    throw "Nedostaju obavezne vrednosti u .env: $($missingVariables -join ', ')."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK nije dostupan. Instalirajte .NET 10 SDK i proverite PATH."
}
if (-not (Get-Command node -ErrorAction SilentlyContinue) -or
    -not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw "Node.js/npm nije dostupan. Instalirajte Node.js i proverite PATH."
}

$nodeExecutable = (Get-Command node).Source
$viteScript = Join-Path $repoRoot "frontend\node_modules\vite\bin\vite.js"
if (-not (Test-Path -LiteralPath $viteScript)) {
    throw "Vite nije instaliran. Pokrenite npm ci --prefix frontend."
}

function Test-PortInUse {
    param([int]$Port)

    $ipProperties = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    return $ipProperties.GetActiveTcpListeners().Port -contains $Port
}

foreach ($port in @(5173, 5238)) {
    if (Test-PortInUse -Port $port) {
        throw "Port $port je vec zauzet. Zaustavite postojeci lokalni proces ili Docker stack."
    }
}

$mysqlHost = if ($env:LOCAL_MYSQL_HOST) { $env:LOCAL_MYSQL_HOST } else { "localhost" }
$mysqlPort = 3306
if ($env:LOCAL_MYSQL_PORT -and
    (-not [int]::TryParse($env:LOCAL_MYSQL_PORT, [ref]$mysqlPort) -or
     $mysqlPort -lt 1 -or $mysqlPort -gt 65535)) {
    throw "LOCAL_MYSQL_PORT mora biti broj izmedju 1 i 65535."
}

$tcpClient = [System.Net.Sockets.TcpClient]::new()
try {
    $connectTask = $tcpClient.ConnectAsync($mysqlHost, $mysqlPort)
    if (-not $connectTask.Wait(3000) -or -not $tcpClient.Connected) {
        throw "MySQL port nije dostupan."
    }
}
catch {
    throw "Local MySQL nije dostupan na ${mysqlHost}:${mysqlPort}.`nPokrenite MySQL servis ili proverite lokalnu konfiguraciju."
}
finally {
    $tcpClient.Dispose()
}

$databaseName = if ($env:MYSQL_DATABASE) { $env:MYSQL_DATABASE } else { "padel_booking" }
$databaseUser = if ($env:MYSQL_USER) { $env:MYSQL_USER } else { "padel_user" }
$jwtIssuer = if ($env:JWT_ISSUER) { $env:JWT_ISSUER } else { "PadelBooking.Api" }
$jwtAudience = if ($env:JWT_AUDIENCE) { $env:JWT_AUDIENCE } else { "PadelBooking.Client" }

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5238"
$env:ConnectionStrings__DefaultConnection = "Server=$mysqlHost;Port=$mysqlPort;Database=$databaseName;User=$databaseUser;Password=$env:MYSQL_PASSWORD"
$env:Jwt__Key = $env:JWT_KEY
$env:Jwt__Issuer = $jwtIssuer
$env:Jwt__Audience = $jwtAudience
if ([string]::IsNullOrWhiteSpace($env:STRIPE_FRONTEND_URL)) {
    $env:STRIPE_FRONTEND_URL = "http://localhost:5173"
}
$env:VITE_API_URL = ""

$emailMappings = @{
    "EMAIL_SMTP_HOST" = "Email__SmtpHost"
    "EMAIL_SMTP_PORT" = "Email__SmtpPort"
    "EMAIL_USERNAME" = "Email__Username"
    "EMAIL_PASSWORD" = "Email__Password"
    "EMAIL_FROM_EMAIL" = "Email__FromEmail"
    "EMAIL_FROM_NAME" = "Email__FromName"
    "EMAIL_USE_SSL" = "Email__UseSsl"
}
foreach ($sourceName in $emailMappings.Keys) {
    $value = [Environment]::GetEnvironmentVariable($sourceName, "Process")
    if ($null -ne $value) {
        [Environment]::SetEnvironmentVariable($emailMappings[$sourceName], $value, "Process")
    }
}

$backendProcess = $null
$frontendProcess = $null
Set-Location -LiteralPath $repoRoot

function Stop-StartedProcessTree {
    param([System.Diagnostics.Process]$Process)

    if ($Process -and -not $Process.HasExited) {
        & taskkill.exe /PID $Process.Id /T /F 2>$null | Out-Null
    }
}

function Wait-HttpEndpoint {
    param(
        [string]$Url,
        [System.Diagnostics.Process]$Process,
        [string]$ServiceName,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            $Process.WaitForExit()
            $Process.Refresh()
            throw "$ServiceName je zavrsen pre nego sto je postao dostupan."
        }

        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    throw "$ServiceName nije postao dostupan u roku od $TimeoutSeconds sekundi."
}

$emptyInputPath = [System.IO.Path]::GetTempFileName()

try {
    Write-Host "Pokretanje API-ja na http://localhost:5238"
    $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
        "run",
        "--project", "backend/src/PadelBooking.Api/PadelBooking.Api.csproj",
        "--no-launch-profile"
    ) -WorkingDirectory $repoRoot -RedirectStandardInput $emptyInputPath -NoNewWindow -PassThru

    Wait-HttpEndpoint `
        -Url "http://localhost:5238/api/courts" `
        -Process $backendProcess `
        -ServiceName "Backend"

    Write-Host "Pokretanje Vite aplikacije na http://localhost:5173"
    $frontendProcess = Start-Process -FilePath $nodeExecutable -ArgumentList @(
        ('"' + $viteScript + '"'),
        "--host", "localhost",
        "--port", "5173",
        "--strictPort"
    ) -WorkingDirectory (Join-Path $repoRoot "frontend") -RedirectStandardInput $emptyInputPath -NoNewWindow -PassThru

    Wait-HttpEndpoint `
        -Url "http://localhost:5173" `
        -Process $frontendProcess `
        -ServiceName "Frontend"

    if (-not [string]::IsNullOrWhiteSpace($env:PADELBOOKING_MOBILE_URL)) {
        Wait-HttpEndpoint `
            -Url $env:PADELBOOKING_MOBILE_URL `
            -Process $frontendProcess `
            -ServiceName "Cloudflare tunel"

        Write-Host ""
        Write-Host "=================================================="
        Write-Host "PadelBooking Mobile Development"
        Write-Host "=================================================="
        Write-Host ""
        Write-Host "Frontend:"
        Write-Host "http://localhost:5173"
        Write-Host ""
        Write-Host "Backend:"
        Write-Host "http://localhost:5238"
        Write-Host ""
        Write-Host "Mobile HTTPS:"
        Write-Host $env:PADELBOOKING_MOBILE_URL
        Write-Host ""
        Write-Host "Otvorite Mobile HTTPS URL na telefonu."
        Write-Host ""
        Write-Host "Pritisnite Ctrl+C za zaustavljanje."
        Write-Host "=================================================="
    }
    else {
        Write-Host "Full local razvoj je spreman. Pritisnite Ctrl+C za zaustavljanje."
    }

    while (-not $backendProcess.HasExited -and -not $frontendProcess.HasExited) {
        Start-Sleep -Seconds 1
        $backendProcess.Refresh()
        $frontendProcess.Refresh()
    }

    if ($backendProcess.HasExited) {
        $backendProcess.WaitForExit()
        $backendProcess.Refresh()
        if ($backendProcess.ExitCode -ne 0) {
            throw "Lokalni backend je zavrsen sa kodom $($backendProcess.ExitCode). Proverite da baza '$databaseName' postoji i da korisnik ima odgovarajuce dozvole."
        }
    }
    if ($frontendProcess.HasExited) {
        $frontendProcess.WaitForExit()
        $frontendProcess.Refresh()
        if ($frontendProcess.ExitCode -ne 0) {
            throw "Lokalni frontend je zavrsen sa kodom $($frontendProcess.ExitCode)."
        }
    }
}
finally {
    foreach ($process in @($backendProcess, $frontendProcess)) {
        Stop-StartedProcessTree -Process $process
    }
    Remove-Item -LiteralPath $emptyInputPath -Force -ErrorAction SilentlyContinue
    Write-Host "Lokalni frontend i backend su zaustavljeni. Lokalna MySQL baza nije menjana niti obrisana."
}
