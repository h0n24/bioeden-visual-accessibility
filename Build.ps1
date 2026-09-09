param([string]$GamePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'BioEden'))
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GamePath 'BioEden_Data\Managed'
$manifestPath = Join-Path $PSScriptRoot 'manifest.json'
$m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
function Sha([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
$originals = @()
foreach ($f in $m.files) {
    $p = Join-Path $managed ($f.name + '.NoDOF.original')
    if (!(Test-Path -LiteralPath $p)) { $p = Join-Path $managed $f.name }
    if ((Sha $p) -ne $f.original) { throw "Unsupported original: $p" }
    $originals += $p
}
dotnet build (Join-Path $PSScriptRoot 'src\Runtime\BioEden.NoDOF.csproj') -c Release -o (Join-Path $PSScriptRoot 'build\runtime') "-p:GameManaged=$managed" "-p:Version=$($m.version)"
if ($LASTEXITCODE) { throw 'Runtime build failed.' }
dotnet build (Join-Path $PSScriptRoot 'src\Patcher\NoDofPatcher.csproj') -c Release -o (Join-Path $PSScriptRoot 'build\patcher')
if ($LASTEXITCODE) { throw 'Patcher build failed.' }
$runtimeBuild = Join-Path $PSScriptRoot 'build\runtime\BioEden.NoDOF.dll'
& (Join-Path $PSScriptRoot 'build\patcher\NoDofPatcher.exe') $originals[0] $originals[1] $runtimeBuild (Join-Path $PSScriptRoot 'build\patched')
if ($LASTEXITCODE) { throw 'Patch generation failed.' }
Copy-Item -LiteralPath $runtimeBuild -Destination (Join-Path $PSScriptRoot 'bin') -Force
foreach ($n in @('NoDofPatcher.exe','NoDofPatcher.exe.config','Mono.Cecil.dll')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('build\patcher\' + $n)) -Destination (Join-Path $PSScriptRoot 'bin') -Force
}
foreach ($f in $m.files) { $f.patched = Sha (Join-Path $PSScriptRoot ('build\patched\' + $f.name)) }
$m.runtime = Sha (Join-Path $PSScriptRoot 'bin\BioEden.NoDOF.dll')
foreach ($p in $m.payload) { $p.sha256 = Sha (Join-Path $PSScriptRoot $p.path) }
$json = $m | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($manifestPath, $json, [Text.UTF8Encoding]::new($false))
Write-Host 'Build complete. No live game files changed. Re-run validation before installation.'
