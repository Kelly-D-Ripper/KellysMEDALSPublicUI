param(
    [string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option' })
)
$ErrorActionPreference = 'Stop'
 $root = $PSScriptRoot
$managed = Join-Path $GameDir 'NuclearOption_Data\Managed'
$core = Join-Path $GameDir 'BepInEx\core'
$csc = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$outDir = Join-Path $root 'bin\Release'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$references = @('mscorlib','System','System.Core','netstandard','Assembly-CSharp','Mirage','UnityEngine','UnityEngine.CoreModule','UnityEngine.UIModule','UnityEngine.UI','Unity.TextMeshPro') | ForEach-Object { Join-Path $managed ($_ + '.dll') }
$references += @((Join-Path $core 'BepInEx.dll'),(Join-Path $core '0Harmony.dll'))
$arguments = @('/nologo','/nostdlib+','/target:library','/langversion:latest','/optimize+','/nullable:enable',"/out:$(Join-Path $outDir 'KellysMEDALSPublicUI.dll')")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += Get-ChildItem -LiteralPath $root -Filter '*.cs' | ForEach-Object { $_.FullName }

& $csc @arguments
if ($LASTEXITCODE -ne 0) { throw 'MEDALS client build failed.' }
Write-Host 'Built optional client-only KellysMEDALSPublicUI.dll'
