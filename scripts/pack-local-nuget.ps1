$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$version = "0.1.0-dev.$timestamp"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\PanguEngine\PanguEngine.csproj"
$outputPath = Join-Path $repoRoot "LocalNuGet"

if (-not (Test-Path $outputPath))
{
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

Get-ChildItem -LiteralPath $outputPath -Filter "PanguEngine.*.nupkg" | Remove-Item -Force
Get-ChildItem -LiteralPath $outputPath -Filter "PanguEngine.*.snupkg" | Remove-Item -Force

Write-Host "Packing PanguEngine $version..."
dotnet pack $projectPath -c Debug -p:PackageVersion=$version -o $outputPath --nologo

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Pack failed."
    exit 1
}

Write-Host ""
Write-Host "Packed PanguEngine $version to $outputPath"
