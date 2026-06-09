$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$modRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $modRoot "ExampleMod.csproj"

Write-Host "Restoring ExampleMod packages with --force..."
dotnet restore $projectPath --force --nologo

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Restore failed."
    exit 1
}

Write-Host "Restored ExampleMod packages."
