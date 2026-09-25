param(
    [switch]$ImportIntoEmptyLocalDatabase
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot ".env"
$backupDirectory = Join-Path $repoRoot ".local-backups"

if (-not (Test-Path -LiteralPath $envPath)) {
    throw "Nedostaje root .env fajl."
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

$databaseName = if ($env:MYSQL_DATABASE) { $env:MYSQL_DATABASE } else { "padel_booking" }
$databaseUser = if ($env:MYSQL_USER) { $env:MYSQL_USER } else { "padel_user" }
$localHost = if ($env:LOCAL_MYSQL_HOST) { $env:LOCAL_MYSQL_HOST } else { "localhost" }
$localPort = if ($env:LOCAL_MYSQL_PORT) { $env:LOCAL_MYSQL_PORT } else { "3306" }

if ($databaseName -notmatch '^[A-Za-z0-9_]+$') {
    throw "MYSQL_DATABASE sadrzi nedozvoljene znakove."
}
if ([string]::IsNullOrWhiteSpace($env:MYSQL_PASSWORD)) {
    throw "MYSQL_PASSWORD nije konfigurisan u .env."
}

$mysqlCommand = Get-Command mysql.exe -ErrorAction SilentlyContinue
$dumpCommand = Get-Command mysqldump.exe -ErrorAction SilentlyContinue
$mysqlExecutable = if ($mysqlCommand) { $mysqlCommand.Source } else { $null }
$dumpExecutable = if ($dumpCommand) { $dumpCommand.Source } else { $null }

if (-not $mysqlExecutable -or -not $dumpExecutable) {
    $defaultMySqlBin = Join-Path $env:ProgramFiles "MySQL\MySQL Server 8.4\bin"
    $defaultMysql = Join-Path $defaultMySqlBin "mysql.exe"
    $defaultDump = Join-Path $defaultMySqlBin "mysqldump.exe"
    if (Test-Path -LiteralPath $defaultMysql) { $mysqlExecutable = $defaultMysql }
    if (Test-Path -LiteralPath $defaultDump) { $dumpExecutable = $defaultDump }
}

if (-not $mysqlExecutable -or -not $dumpExecutable) {
    throw "mysql.exe i mysqldump.exe nisu pronadjeni u PATH-u niti u standardnom MySQL 8.4 direktorijumu."
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker nije dostupan. Ovaj pomocni alat je namenjen samo jednokratnom izvozu stare Docker baze."
}

Set-Location -LiteralPath $repoRoot
Write-Host "Pokretanje iskljucivo legacy Docker MySQL servisa..."
& docker compose --profile legacy-db up -d mysql | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Legacy Docker MySQL nije mogao da se pokrene." }

$containerId = (& docker compose --profile legacy-db ps -q mysql).Trim()
if (-not $containerId) { throw "Legacy Docker MySQL kontejner nije pronadjen." }

$healthy = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    $health = (& docker inspect --format '{{.State.Health.Status}}' $containerId 2>$null).Trim()
    if ($health -eq "healthy") {
        $healthy = $true
        break
    }
    Start-Sleep -Seconds 2
}
if (-not $healthy) { throw "Legacy Docker MySQL nije postao healthy u ocekivanom roku." }

New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
$dockerExport = Join-Path $backupDirectory "docker-$databaseName-$timestamp.sql"
$localBackup = Join-Path $backupDirectory "local-$databaseName-$timestamp.sql"
$previousMysqlPassword = $env:MYSQL_PWD

try {
    $env:MYSQL_PWD = $env:MYSQL_PASSWORD

    Write-Host "Izvoz stare Docker baze u lokalni backup fajl..."
    & $dumpExecutable `
        --host=127.0.0.1 `
        --port=3307 `
        --protocol=TCP `
        --user=$databaseUser `
        --single-transaction `
        --routines `
        --triggers `
        --no-tablespaces `
        --skip-add-drop-table `
        $databaseName `
        --result-file=$dockerExport
    if ($LASTEXITCODE -ne 0) { throw "Izvoz Docker baze nije uspeo." }

    $tableCountOutput = & $mysqlExecutable `
        --host=$localHost `
        --port=$localPort `
        --protocol=TCP `
        --user=$databaseUser `
        --batch `
        --skip-column-names `
        --execute="SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$databaseName';"
    if ($LASTEXITCODE -ne 0) {
        throw "Lokalna baza nije dostupna sa podesavanjima iz .env. Docker izvoz je sacuvan u '$dockerExport'."
    }

    $tableCount = [int](($tableCountOutput | Select-Object -Last 1).Trim())
    if ($tableCount -gt 0) {
        Write-Host "Lokalna baza sadrzi tabele; pravljenje bezbednosnog backupa pre prekida..."
        & $dumpExecutable `
            --host=$localHost `
            --port=$localPort `
            --protocol=TCP `
            --user=$databaseUser `
            --single-transaction `
            --routines `
            --triggers `
            --no-tablespaces `
            $databaseName `
            --result-file=$localBackup
        if ($LASTEXITCODE -ne 0) { throw "Backup lokalne baze nije uspeo; import nije pokrenut." }

        throw "Lokalna baza vec sadrzi $tableCount tabela. Automatski import je odbijen da podaci ne bi bili prepisani. Docker izvoz: '$dockerExport'. Lokalni backup: '$localBackup'. Podatke spojite rucno nakon pregleda."
    }

    if (-not $ImportIntoEmptyLocalDatabase) {
        Write-Host "Docker izvoz je sacuvan u '$dockerExport'."
        Write-Host "Lokalna baza je prazna. Pregledajte backup, zatim ponovite komandu sa -ImportIntoEmptyLocalDatabase za eksplicitni import."
        exit 0
    }

    Write-Host "Import u potvrdjeno praznu lokalnu bazu..."
    $importProcess = Start-Process -FilePath $mysqlExecutable -ArgumentList @(
        "--host=$localHost",
        "--port=$localPort",
        "--protocol=TCP",
        "--user=$databaseUser",
        $databaseName
    ) -RedirectStandardInput $dockerExport -NoNewWindow -Wait -PassThru
    if ($importProcess.ExitCode -ne 0) { throw "Import u lokalnu bazu nije uspeo. Docker izvoz je sacuvan." }

    Write-Host "Migracija je zavrsena. Stari Docker volume nije obrisan."
}
finally {
    $env:MYSQL_PWD = $previousMysqlPassword
}
