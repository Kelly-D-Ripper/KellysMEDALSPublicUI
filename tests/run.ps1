param([string]$Compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$testExe = Join-Path $outDir 'ClientLogicTests.exe'
& $Compiler /nologo /target:exe /langversion:latest /optimize+ "/out:$testExe" (Join-Path $root 'MedalsUiProtocol.cs') (Join-Path $root 'MfdSlotLease.cs') (Join-Path $root 'MfdPanelLayout.cs') (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Client logic tests failed to compile.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Client logic tests failed.' }
