param(
    [string] $GamePath = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$modRoot = Split-Path -Parent $scriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)

if ([string]::IsNullOrWhiteSpace($GamePath))
{
    $appOutputPath = Join-Path $repoRoot "src\PanguEngine.App\bin\Debug\net10.0"
    $appPath = Join-Path $appOutputPath "PanguEngine.App.exe"
}
else
{
    $appPath = [System.IO.Path]::GetFullPath($GamePath)
    $appOutputPath = Split-Path -Parent $appPath
}

$modOutputPath = Join-Path $modRoot "bin\Debug\net10.0"
$propertiesPath = Join-Path $modRoot "Properties"
$launchSettingsPath = Join-Path $propertiesPath "launchSettings.json"

if (-not (Test-Path -LiteralPath $propertiesPath))
{
    New-Item -ItemType Directory -Path $propertiesPath -Force | Out-Null
}

function ConvertTo-JsonLiteral
{
    param([string] $Value)

    return $Value.Replace("\", "\\").Replace('"', '\"')
}

$appPathJson = ConvertTo-JsonLiteral $appPath
$appOutputPathJson = ConvertTo-JsonLiteral $appOutputPath
$modOutputPathJson = ConvertTo-JsonLiteral $modOutputPath

$launchSettings = @"
{
  "profiles": {
    "PanguEngine.App": {
      "commandName": "Executable",
      "executablePath": "$appPathJson",
      "commandLineArgs": "--mod \"$modOutputPathJson\"",
      "workingDirectory": "$appOutputPathJson"
    }
  }
}
"@

Set-Content -LiteralPath $launchSettingsPath -Value $launchSettings -Encoding UTF8

Write-Host "Wrote $launchSettingsPath"
