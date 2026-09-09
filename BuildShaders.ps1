param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.4.5f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'build/WaterShader'
New-Item -ItemType Directory -Force (Join-Path $project 'Assets/Editor') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'src/Shaders/NeutralWaterPixels.shader') (Join-Path $project 'Assets') -Force
Copy-Item (Join-Path $PSScriptRoot 'src/Shaders/BuildWaterShader.cs') (Join-Path $project 'Assets/Editor') -Force
$log = Join-Path $PSScriptRoot 'build/water-shader-build.log'
$arguments = '-batchmode -nographics -quit -projectPath "{0}" -executeMethod BuildWaterShader.Build -logFile "{1}"' -f $project, $log
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Shader build failed. See $log" }
Copy-Item (Join-Path $project 'Output/neutralwater') (Join-Path $PSScriptRoot 'src/Shaders/neutralwater') -Force
Write-Host 'Shader bundle rebuilt. Run Build.ps1 next.'
