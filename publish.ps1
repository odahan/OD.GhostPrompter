[CmdletBinding()]
param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$projectFile = Join-Path $PSScriptRoot 'src\GhostPrompter\GhostPrompter.csproj'
$publishDirectory = Join-Path $PSScriptRoot 'bin\publish'

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$publishArguments = @('publish', $projectFile, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-m:1', '-o', $publishDirectory)
if ($NoRestore) {
    $publishArguments += '--no-restore'
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publishing GhostPrompter failed with exit code $LASTEXITCODE."
}

$executable = Join-Path $publishDirectory 'GhostPrompter.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "The expected executable was not created: $executable"
}

Write-Host "GhostPrompter was published to $executable"
