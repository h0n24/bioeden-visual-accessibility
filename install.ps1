param(
    [string]$GamePath,
    [switch]$Uninstall,
    [switch]$Status
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Find-BioEden {
    $candidates = @()
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'Steam\steamapps\common\BioEden' }
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\BioEden' }
    $candidates += Join-Path (Split-Path $PSScriptRoot -Parent) 'BioEden'
    $candidates += 'D:\Games\BioEden'
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'BioEden.exe')) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    return $null
}

if ([string]::IsNullOrWhiteSpace($GamePath)) { $GamePath = Find-BioEden }
if ([string]::IsNullOrWhiteSpace($GamePath) -or !(Test-Path -LiteralPath (Join-Path $GamePath 'BioEden.exe'))) {
    $GamePath = Read-Host 'Enter the folder containing BioEden.exe'
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$action = if ($Uninstall) { 'Uninstall' } elseif ($Status) { 'Status' } else { 'Install' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'NoDOF.ps1') -Action $action -GamePath $GamePath
exit $LASTEXITCODE
