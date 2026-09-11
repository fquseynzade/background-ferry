param([ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    $destination = Join-Path "$PWD" "artifacts/BackgroundFerry-$Runtime"
    dotnet publish src/BackgroundFerry.App -c Release -r $Runtime --self-contained true -o $destination -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item LICENSE, README.md, README.ru.md, CONTRIBUTING.md -Destination $destination
    Copy-Item docs -Destination $destination -Recurse -Force
    Copy-Item chrome-extension -Destination $destination -Recurse -Force
    Compress-Archive -Path "chrome-extension/*" -DestinationPath "artifacts/BackgroundFerry-Chrome.zip" -Force
    Compress-Archive -Path "$destination/*" -DestinationPath "artifacts/BackgroundFerry-$Runtime.zip" -Force
    Get-FileHash "artifacts/BackgroundFerry-$Runtime.zip" -Algorithm SHA256 | Format-List
} finally { Pop-Location }
