param([switch]$Integration)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    dotnet build BackgroundFerry.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet run --project tests/BackgroundFerry.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    if ($Integration) {
        $testState = Join-Path ([System.IO.Path]::GetTempPath()) ('BackgroundFerry.Tests-' + [guid]::NewGuid())
        dotnet run --project tests/BackgroundFerry.Integration -c Release --no-build -- "$PWD" $testState
        if ($LASTEXITCODE -ne 0) { throw 'Windows integration tests failed' }
    }
} finally { Pop-Location }
