param(
    [ValidateSet('Install','Uninstall','Status')][string]$Action = 'Status',
    [string]$GamePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'BioEden')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
trap { Write-Host ("ERROR: " + $_.Exception.Message) -ForegroundColor Red; exit 1 }
function Hash([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $s = [IO.File]::OpenRead($Path)
    try { [BitConverter]::ToString($sha.ComputeHash($s)).Replace('-','') }
    finally { $s.Dispose(); $sha.Dispose() }
}
function CheckClosed {
    if (Get-Process -Name BioEden -ErrorAction SilentlyContinue) { throw 'Close BioEden before modifying the installation.' }
}
$m = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$game = (Resolve-Path -LiteralPath $GamePath).Path
if (!(Test-Path -LiteralPath (Join-Path $game 'BioEden.exe'))) { throw 'BioEden.exe not found.' }
$managed = Join-Path $game 'BioEden_Data\Managed'
$runtime = Join-Path $managed 'BioEden.NoDOF.dll'
$allPatched = $true
$allOriginal = $true
foreach ($f in $m.files) {
    $path = Join-Path $managed $f.name
    $h = Hash $path
    if ($h -ne $f.patched) { $allPatched = $false }
    if ($h -ne $f.original) { $allOriginal = $false }
    $allowed = @($f.original, $f.patched) + @($f.legacy)
    if ($h -notin $allowed) { throw ("Unsupported or changed library: " + $f.name + '. No game files changed.') }
    $backup = "$path.NoDOF.original"
    if ((Test-Path -LiteralPath $backup) -and (Hash $backup) -ne $f.original) { throw "Original backup verification failed: $backup" }
    if ($h -ne $f.original -and !(Test-Path -LiteralPath $backup)) { throw "Original backup is missing: $backup" }
}
$runtimeExists = Test-Path -LiteralPath $runtime
$runtimeCurrent = if ($runtimeExists) { Hash $runtime } else { '' }
if ($runtimeExists -and $runtimeCurrent -notin (@($m.runtime) + @($m.previousRuntime))) { throw 'The existing mod runtime has an unknown version. No game files changed.' }
if ($Action -eq 'Status') {
    if ($allPatched -and $runtimeCurrent -eq $m.runtime) { Write-Host 'BioEden visual accessibility 1.3.0 is INSTALLED. Settings: Video > Depth of Field; Accessibility > Antenna Range Outline / Extended Zoom / Map Desaturation Filter / Map Filter Hotkey.' }
    elseif ($allOriginal -and !$runtimeExists) { Write-Host 'NoDOF menu toggle is NOT installed. Game libraries are original.' }
    else { Write-Host 'Legacy or partial NoDOF installation detected. Install upgrades it; Uninstall restores the game.' }
    return
}
CheckClosed
if ($Action -eq 'Install' -and $allPatched -and $runtimeCurrent -eq $m.runtime) { Write-Host 'Visual accessibility 1.3.0 is already installed and verified.'; return }
if ($Action -eq 'Uninstall' -and $allOriginal -and !$runtimeExists) { Write-Host 'Original game is already restored.'; return }
if ($Action -eq 'Install') {
    foreach ($p in $m.payload) {
        if ((Hash (Join-Path $PSScriptRoot $p.path)) -ne $p.sha256) { throw ("Package verification failed: " + $p.path) }
    }
}

# Backups are immutable originals, shared with the older fixed-Off version.
foreach ($f in $m.files) {
    $path = Join-Path $managed $f.name
    $backup = "$path.NoDOF.original"
    if (!(Test-Path -LiteralPath $backup)) { [IO.File]::Copy($path, $backup, $false) }
    if ((Hash $backup) -ne $f.original) { throw 'Original backup verification failed.' }
}
$stage = Join-Path $managed ('.NoDOF-stage-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage) | Out-Null
$committed = New-Object 'System.Collections.Generic.List[string]'
$runtimeAdded = $false
$runtimeReplaced = $false
try {
    foreach ($f in $m.files) { [IO.File]::Copy((Join-Path $managed $f.name), (Join-Path $stage ($f.name + '.before')), $false) }
    if ($runtimeExists) { [IO.File]::Copy($runtime, (Join-Path $stage 'BioEden.NoDOF.dll.before'), $false) }
    if ($Action -eq 'Install') {
        & (Join-Path $PSScriptRoot 'bin\NoDofPatcher.exe') (Join-Path $managed 'Assembly-CSharp.dll.NoDOF.original') (Join-Path $managed 'Unity.RenderPipelines.Universal.Runtime.dll.NoDOF.original') (Join-Path $PSScriptRoot 'bin\BioEden.NoDOF.dll') $stage
        if ($LASTEXITCODE -ne 0) { throw 'Patch generation failed. Game libraries were not changed.' }
        foreach ($f in $m.files) { if ((Hash (Join-Path $stage $f.name)) -ne $f.patched) { throw 'Generated patch hash mismatch.' } }
        [IO.File]::Copy((Join-Path $PSScriptRoot 'bin\BioEden.NoDOF.dll'), (Join-Path $stage 'BioEden.NoDOF.dll'), $false)
    } else {
        foreach ($f in $m.files) { [IO.File]::Copy((Join-Path $managed ($f.name + '.NoDOF.original')), (Join-Path $stage $f.name), $false) }
    }
    CheckClosed
    foreach ($f in $m.files) {
        if ((Hash (Join-Path $managed $f.name)) -ne (Hash (Join-Path $stage ($f.name + '.before')))) { throw 'A game library changed while preparing the operation.' }
    }
    if ($Action -eq 'Install' -and !$runtimeExists) {
        [IO.File]::Move((Join-Path $stage 'BioEden.NoDOF.dll'), $runtime)
        $runtimeAdded = $true
    } elseif ($Action -eq 'Install') {
        if ((Hash $runtime) -ne $runtimeCurrent) { throw 'Mod runtime changed during preparation.' }
        [IO.File]::Replace((Join-Path $stage 'BioEden.NoDOF.dll'), $runtime, [NullString]::Value)
        $runtimeReplaced = $true
    }
    foreach ($f in $m.files) {
        $target = Join-Path $managed $f.name
        [IO.File]::Replace((Join-Path $stage $f.name), $target, [NullString]::Value)
        $committed.Add($f.name)
        $expected = if ($Action -eq 'Install') { $f.patched } else { $f.original }
        if ((Hash $target) -ne $expected) { throw 'Installed library verification failed.' }
    }
    if ($Action -eq 'Uninstall' -and $runtimeExists) { [IO.File]::Delete($runtime) }
    Write-Host ("SUCCESS: " + $Action + ' complete. Original backups retained.') -ForegroundColor Green
} catch {
    $failure = $_
    foreach ($name in $committed) { [IO.File]::Replace((Join-Path $stage ($name + '.before')), (Join-Path $managed $name), [NullString]::Value) }
    if ($runtimeAdded -and (Test-Path -LiteralPath $runtime) -and (Hash $runtime) -eq $m.runtime) { [IO.File]::Delete($runtime) }
    if ($runtimeReplaced) { [IO.File]::Replace((Join-Path $stage 'BioEden.NoDOF.dll.before'), $runtime, [NullString]::Value) }
    throw $failure
} finally {
    # Delete only this operation's individually named staging files; never recurse.
    $stageFiles = @('BioEden.NoDOF.dll', 'BioEden.NoDOF.dll.before')
    foreach ($f in $m.files) { $stageFiles += $f.name; $stageFiles += ($f.name + '.before') }
    foreach ($name in $stageFiles) {
        $path = Join-Path $stage $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { [IO.File]::Delete($path) }
    }
    if ([IO.Directory]::GetFileSystemEntries($stage).Length -eq 0) { [IO.Directory]::Delete($stage, $false) }
}


