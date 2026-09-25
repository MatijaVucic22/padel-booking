$ErrorActionPreference = "Stop"

$noDockerScript = Join-Path $PSScriptRoot "dev-no-docker.ps1"
& $noDockerScript
