$connectionString = [Environment]::GetEnvironmentVariable("PADELBOOKING_BENCHMARK_CONNECTION_STRING", "Process")
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    Write-Host "Nedostaje PADELBOOKING_BENCHMARK_CONNECTION_STRING. Podesite je pre pokretanja benchmarka."
    exit 1
}

$projectPath = Join-Path $PSScriptRoot "PadelBooking.Benchmarks"
$resultsPath = Join-Path $projectPath "BenchmarkDotNet.Artifacts\results"
$logsPath = Join-Path $PSScriptRoot "logs"
$historyPath = Join-Path $PSScriptRoot "history"
$summaryPath = Join-Path $PSScriptRoot "benchmark-summary.html"
New-Item -ItemType Directory -Force -Path $logsPath, $historyPath | Out-Null

function Invoke-BenchmarkRun {
    param([string]$Filter, [string]$LogPath, [string]$FailureMessage)

    & dotnet run --configuration Release -- --filter $Filter *> $LogPath
    if ($LASTEXITCODE -eq 0) { return }

    Write-Host $FailureMessage
    Write-Host "Detalji su u: $LogPath"
    Get-Content -LiteralPath $LogPath -Tail 20
    exit $LASTEXITCODE
}

function Convert-Measure {
    param([string]$Value, [ValidateSet("Time", "Memory")][string]$Kind)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -eq "?") { return $null }
    if ($Value -notmatch "^\s*([0-9.,]+)\s*(.*)$") { return $null }

    $numberText = $matches[1]
    if ($numberText.Contains(",") -and $numberText.Contains(".")) { $numberText = $numberText.Replace(",", "") }
    elseif ($numberText.Contains(",")) { $numberText = $numberText.Replace(",", ".") }

    $number = 0.0
    if (-not [double]::TryParse($numberText, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) { return $null }

    $unit = $matches[2].Trim().ToLowerInvariant()
    if ($Kind -eq "Time") {
        if ($unit -eq "s") { return $number * 1000 }
        if ($unit -match "^ms") { return $number }
        if ($unit -match "^ns") { return $number / 1000000 }
        if ($unit -match "s$") { return $number / 1000 }
        return $null
    }

    if ($unit -match "^mb") { return $number * 1024 }
    if ($unit -match "^kb") { return $number }
    if ($unit -match "^b") { return $number / 1024 }
    return $null
}

function Get-Record { param([object[]]$Rows, [string]$Method) return $Rows | Where-Object Method -eq $Method | Select-Object -First 1 }

function Get-ComparisonLine {
    param([string]$Label, [double]$Baseline, [double]$Compared, [switch]$Speedup)
    if ($Baseline -le 0 -or $Compared -le 0) { return $null }
    if ($Speedup) { return "${Label} je {0:N1}x brzi." -f ($Baseline / $Compared) }

    $difference = (($Compared - $Baseline) / $Baseline) * 100
    if ($difference -lt 0) { return "${Label}: {0:N1}% manje." -f [math]::Abs($difference) }
    return "${Label}: {0:N1}% vise." -f $difference
}

function Format-Milliseconds {
    param([string]$Value)

    $milliseconds = Convert-Measure $Value Time
    if ($null -eq $milliseconds) { return "N/A" }
    return "{0:N3} ms" -f $milliseconds
}

function ConvertTo-HtmlTable {
    param([object[]]$Rows, [hashtable]$Names)
    $body = foreach ($row in $Rows) {
        $name = [Net.WebUtility]::HtmlEncode($Names[$row.Method])
        $mean = [Net.WebUtility]::HtmlEncode((Format-Milliseconds $row.Mean))
        $stdDev = [Net.WebUtility]::HtmlEncode((Format-Milliseconds $row.StdDev))
        $ratio = [Net.WebUtility]::HtmlEncode([string]$row.Ratio)
        $allocated = [Net.WebUtility]::HtmlEncode([string]$row.Allocated)
        "<tr><td>$name</td><td>$mean</td><td>$stdDev</td><td>$ratio</td><td>$allocated</td></tr>"
    }
    "<table><thead><tr><th>Metod</th><th>Mean</th><th>StdDev</th><th>Ratio</th><th>Allocated</th></tr></thead><tbody>$($body -join '')</tbody></table>"
}

Push-Location $projectPath
try {
    Write-Host ""
    Write-Host "PADELBOOKING - PERFORMANCE BENCHMARK"
    Write-Host ""
    Write-Host "[1/2] Testiranje pristupa bazi..."
    Write-Host "- EF Core tracked"
    Write-Host "- EF Core AsNoTracking"
    Write-Host "- Dapper"
    Invoke-BenchmarkRun "*ActiveCourtsBenchmarks*" (Join-Path $logsPath "active-courts.log") "Test 1 nije uspesno zavrsen."

    Write-Host ""
    Write-Host "[2/2] Testiranje kesiranja..."
    Write-Host "- EF Core / MySQL"
    Write-Host "- HybridCache cold"
    Write-Host "- HybridCache warm"
    Invoke-BenchmarkRun "*ActiveCourtsCacheBenchmarks*" (Join-Path $logsPath "cache.log") "Test 2 nije uspesno zavrsen."
}
finally {
    Pop-Location
}

$dataAccessCsv = Join-Path $resultsPath "PadelBooking.Benchmarks.ActiveCourtsBenchmarks-report.csv"
$cacheCsv = Join-Path $resultsPath "PadelBooking.Benchmarks.ActiveCourtsCacheBenchmarks-report.csv"
if (-not (Test-Path -LiteralPath $dataAccessCsv) -or -not (Test-Path -LiteralPath $cacheCsv)) {
    Write-Host "CSV izvestaji nisu pronadjeni. Detalji su u: $logsPath"
    exit 1
}

$dataAccessRows = @(Import-Csv -LiteralPath $dataAccessCsv)
$cacheRows = @(Import-Csv -LiteralPath $cacheCsv)
$tracked = Get-Record $dataAccessRows "GetActiveCourtsEfTracked"
$noTracking = Get-Record $dataAccessRows "GetActiveCourtsEfAsNoTracking"
$dapper = Get-Record $dataAccessRows "GetActiveCourtsDapper"
$databaseRead = Get-Record $cacheRows "GetActiveCourtsEfAsNoTracking"
$coldCache = Get-Record $cacheRows "GetActiveCourtsHybridCacheCold"
$warmCache = Get-Record $cacheRows "GetActiveCourtsHybridCacheWarm"

$dataAccessNames = @{ GetActiveCourtsEfTracked = "EF Core tracked"; GetActiveCourtsEfAsNoTracking = "EF Core AsNoTracking"; GetActiveCourtsDapper = "Dapper" }
$cacheNames = @{ GetActiveCourtsEfAsNoTracking = "EF Core AsNoTracking / MySQL"; GetActiveCourtsHybridCacheCold = "HybridCache cold miss"; GetActiveCourtsHybridCacheWarm = "HybridCache warm hit" }

$comparisons = @()
if ($tracked -and $noTracking) {
    $comparisons += Get-ComparisonLine "AsNoTracking u odnosu na tracked" (Convert-Measure $tracked.Mean Time) (Convert-Measure $noTracking.Mean Time)
    $comparisons += Get-ComparisonLine "AsNoTracking alocira" (Convert-Measure $tracked.Allocated Memory) (Convert-Measure $noTracking.Allocated Memory)
}
if ($tracked -and $dapper) {
    $comparisons += Get-ComparisonLine "Dapper u odnosu na tracked" (Convert-Measure $tracked.Mean Time) (Convert-Measure $dapper.Mean Time)
    $comparisons += Get-ComparisonLine "Dapper alocira" (Convert-Measure $tracked.Allocated Memory) (Convert-Measure $dapper.Allocated Memory)
}
if ($databaseRead -and $warmCache) {
    $comparisons += Get-ComparisonLine "Warm cache u odnosu na direktno DB citanje" (Convert-Measure $databaseRead.Mean Time) (Convert-Measure $warmCache.Mean Time) -Speedup
    $comparisons += Get-ComparisonLine "Warm cache alocira" (Convert-Measure $databaseRead.Allocated Memory) (Convert-Measure $warmCache.Allocated Memory)
}
if ($databaseRead -and $coldCache) {
    $comparisons += Get-ComparisonLine "Cold cache u odnosu na direktno DB citanje" (Convert-Measure $databaseRead.Mean Time) (Convert-Measure $coldCache.Mean Time)
    $comparisons += Get-ComparisonLine "Cold cache alocira" (Convert-Measure $databaseRead.Allocated Memory) (Convert-Measure $coldCache.Allocated Memory)
}
$comparisonHtml = (($comparisons | Where-Object { $_ } | ForEach-Object { "<li>$([Net.WebUtility]::HtmlEncode($_))</li>" }) -join '')
if ([string]::IsNullOrWhiteSpace($comparisonHtml)) { $comparisonHtml = "<li>Poredjenje nije dostupno za trenutne CSV podatke.</li>" }

$historyLinks = (Get-ChildItem -LiteralPath $historyPath -Filter "*-summary.html" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    ForEach-Object {
        $name = [Net.WebUtility]::HtmlEncode($_.Name)
        ('<li><a href="history/{0}">{0}</a></li>' -f $name)
    }) -join ''
if ([string]::IsNullOrWhiteSpace($historyLinks)) { $historyLinks = "<li>Jos nema arhiviranih izvestaja.</li>" }

$resultsRelativePath = "PadelBooking.Benchmarks/BenchmarkDotNet.Artifacts/results"
$runDate = Get-Date -Format "dd.MM.yyyy. HH:mm"
$report = @"
<!doctype html>
<html lang="sr-Latn-RS">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PadelBooking - Performance Benchmark</title>
  <style>
    body { max-width: 1080px; margin: 40px auto; padding: 0 20px; background: #f5f3ec; color: #17201b; font: 16px/1.55 Arial, sans-serif; }
    h1, h2 { color: #245c46; } h1 { margin-bottom: 4px; } .muted { color: #667068; }
    section { margin-top: 34px; padding: 24px; border: 1px solid #ddd9ce; border-radius: 12px; background: #fff; }
    table { width: 100%; border-collapse: collapse; } th, td { padding: 10px; border-bottom: 1px solid #ddd9ce; text-align: left; } th { color: #245c46; }
    .note { padding: 14px; border-left: 4px solid #c98262; background: #ece9df; } a { color: #245c46; font-weight: 700; }
  </style>
</head>
<body>
  <h1>PadelBooking - Performance Benchmark</h1>
  <p class="muted">Poslednje pokretanje: $runDate</p>
  <section>
    <h2>Test 1 - Pristup podacima</h2>
    <p>Porede se EF Core tracked, EF Core AsNoTracking i Dapper pri citanju aktivnih terena iz iste MySQL baze.</p>
    $(ConvertTo-HtmlTable @($tracked, $noTracking, $dapper | Where-Object { $_ }) $dataAccessNames)
    <p><a href="$resultsRelativePath/PadelBooking.Benchmarks.ActiveCourtsBenchmarks-report.html">Detaljan BenchmarkDotNet HTML izvestaj za Test 1</a></p>
  </section>
  <section>
    <h2>Test 2 - Efekat kesiranja</h2>
    <p>Porede se direktno EF Core/MySQL citanje, HybridCache cold miss i warm hit. HybridCache je zaseban eksperiment kesiranja, a ne ORM konkurent EF Core-u ili Dapper-u.</p>
    $(ConvertTo-HtmlTable @($databaseRead, $coldCache, $warmCache | Where-Object { $_ }) $cacheNames)
    <p><a href="$resultsRelativePath/PadelBooking.Benchmarks.ActiveCourtsCacheBenchmarks-report.html">Detaljan BenchmarkDotNet HTML izvestaj za Test 2</a></p>
  </section>
  <section>
    <h2>Kako citati rezultate</h2>
    <p><strong>Mean</strong> je prosecno vreme izvrsavanja, <strong>StdDev</strong> pokazuje varijaciju rezultata, <strong>Ratio</strong> je odnos prema BenchmarkDotNet referentnom metodu, a <strong>Allocated</strong> je alocirana managed memorija.</p>
    <div class="note"><strong>Izracunato iz trenutnih CSV rezultata:</strong><ul>$comparisonHtml</ul></div>
  </section>
  <section><h2>Prethodni testovi</h2><ul>$historyLinks</ul></section>
</body>
</html>
"@

Set-Content -LiteralPath $summaryPath -Value $report -Encoding utf8
$historyFileName = "{0}-summary.html" -f (Get-Date -Format "yyyy-MM-dd_HHmm")
Copy-Item -LiteralPath $summaryPath -Destination (Join-Path $historyPath $historyFileName) -Force
Get-ChildItem -LiteralPath $historyPath -Filter "*-summary.html" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip 10 |
    Remove-Item -Force

Write-Host ""
Write-Host "Izvestaj: $summaryPath"
Write-Host "Detaljni BenchmarkDotNet rezultati: $resultsPath"
try { Start-Process -FilePath $summaryPath }
catch { Write-Host "Izvestaj nije bilo moguce automatski otvoriti. Otvorite ga rucno: $summaryPath" }
