param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'WinPilot.csproj'
$output = Join-Path $PSScriptRoot 'publish'

dotnet test (Join-Path $PSScriptRoot 'WinPilot.sln') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

$arguments = @('publish', $project, '-c', $Configuration, '-r', 'win-x64', '-o', $output)
if ($SelfContained) {
    $arguments += @('--self-contained', 'true', '-p:PublishSingleFile=true')
} else {
    $arguments += @('--self-contained', 'false')
}

dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Host "WinPilot published to $output" -ForegroundColor Green
