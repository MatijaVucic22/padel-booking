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

    [Environment]::SetEnvironmentVariable($name, $value, "Process")
}

$requiredVariables = @(
    "MYSQL_PASSWORD",
    "MYSQL_ROOT_PASSWORD",
    "JWT_KEY",
    "STRIPE_SECRET_KEY",
    "STRIPE_WEBHOOK_SECRET",
    "STRIPE_FRONTEND_URL"
)

$missingVariables = $requiredVariables | Where-Object {
    $value = [Environment]::GetEnvironmentVariable($_, "Process")
    [string]::IsNullOrWhiteSpace($value)
}

if ($missingVariables.Count -gt 0) {
    throw "Nedostaju obavezne vrednosti u .env: $($missingVariables -join ', ')."
}

$databaseName = if ($env:MYSQL_DATABASE) { $env:MYSQL_DATABASE } else { "padel_booking" }
$databaseUser = if ($env:MYSQL_USER) { $env:MYSQL_USER } else { "padel_user" }
$jwtIssuer = if ($env:JWT_ISSUER) { $env:JWT_ISSUER } else { "PadelBooking.Api" }
$jwtAudience = if ($env:JWT_AUDIENCE) { $env:JWT_AUDIENCE } else { "PadelBooking.Client" }

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5238"
$env:ConnectionStrings__DefaultConnection = "Server=127.0.0.1;Port=3307;Database=$databaseName;User=$databaseUser;Password=$env:MYSQL_PASSWORD"
$env:Jwt__Key = $env:JWT_KEY
$env:Jwt__Issuer = $jwtIssuer
$env:Jwt__Audience = $jwtAudience
$env:STRIPE_FRONTEND_URL = "http://localhost:5173"
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

Set-Location -LiteralPath $repoRoot

Write-Host "Prebacivanje na lokalni app rezim..."
& docker compose stop frontend backend | Out-Host
& docker compose up -d --no-deps mysql | Out-Host
if ($LASTEXITCODE -ne 0) { throw "MySQL Docker servis nije mogao da se pokrene." }

$mysqlContainerId = (& docker compose ps -q mysql).Trim()
if (-not $mysqlContainerId) { throw "MySQL Docker kontejner nije pronadjen." }

Write-Host "Cekanje da MySQL postane healthy..."
$mysqlHealthy = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    $health = (& docker inspect --format '{{.State.Health.Status}}' $mysqlContainerId 2>$null).Trim()
    if ($health -eq "healthy") {
        $mysqlHealthy = $true
        break
    }
    Start-Sleep -Seconds 2
}

if (-not $mysqlHealthy) { throw "MySQL nije postao healthy u ocekivanom roku." }

Write-Host "Pokretanje API-ja na http://localhost:5238"
$backendProcess = Start-Process -FilePath "dotnet" -ArgumentList @(
    "run",
    "--project", "backend/src/PadelBooking.Api/PadelBooking.Api.csproj",
    "--no-launch-profile"
) -WorkingDirectory $repoRoot -NoNewWindow -PassThru

Write-Host "Pokretanje Vite aplikacije na http://localhost:5173"
$frontendProcess = Start-Process -FilePath "npm.cmd" -ArgumentList @(
    "--prefix", "frontend",
    "run", "dev", "--",
    "--host", "localhost",
    "--port", "5173",
    "--strictPort"
) -WorkingDirectory $repoRoot -NoNewWindow -PassThru

Write-Host "Lokalni razvoj je pokrenut. Pritisnite Ctrl+C za zaustavljanje lokalnih procesa."

try {
    while (-not $backendProcess.HasExited -and -not $frontendProcess.HasExited) {
        Start-Sleep -Seconds 1
        $backendProcess.Refresh()
        $frontendProcess.Refresh()
    }

    if ($backendProcess.HasExited -and $backendProcess.ExitCode -ne 0) {
        throw "Lokalni backend je zavrsen sa kodom $($backendProcess.ExitCode)."
    }
    if ($frontendProcess.HasExited -and $frontendProcess.ExitCode -ne 0) {
        throw "Lokalni frontend je zavrsen sa kodom $($frontendProcess.ExitCode)."
    }
}
finally {
    foreach ($process in @($backendProcess, $frontendProcess)) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Lokalni frontend i backend su zaustavljeni. MySQL i njegovi podaci ostaju netaknuti."
}
