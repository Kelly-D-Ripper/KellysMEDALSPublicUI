param([string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option' }))
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1') -GameDir $GameDir
& (Join-Path $root 'tests/run.ps1')
& (Join-Path $root 'tests/Test-GameApi.ps1') -GameDir $GameDir -SelfTest
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist 'KellysMEDALSPublicUI-0.1.3-CLIENT.zip'
if (Test-Path -LiteralPath $zip) { throw 'Release archive already exists; preserve it or choose a new version.' }
$files = [ordered]@{
    'BepInEx/plugins/KellysMEDALSPublicUI/KellysMEDALSPublicUI.dll' = 'bin/Release/KellysMEDALSPublicUI.dll'
    'README.md' = 'README.md'
    'RELEASE-NOTES.md' = 'RELEASE-NOTES.md'
    'COMPATIBILITY.md' = 'COMPATIBILITY.md'
    'PERFORMANCE.md' = 'PERFORMANCE.md'
    'LICENSE' = 'LICENSE'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($entry in $files.GetEnumerator()) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $root $entry.Value), $entry.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries.Count -ne $files.Count) { throw 'Unexpected package inventory.' }
    foreach ($entry in $archive.Entries) {
        if (!$files.Contains($entry.FullName)) { throw "Unexpected package entry: $($entry.FullName)" }
        $sha = [Security.Cryptography.SHA256]::Create()
        $source = $entry.Open()
        try { $digest = [BitConverter]::ToString($sha.ComputeHash($source)).Replace('-','') }
        finally { $source.Dispose(); $sha.Dispose() }
        if ($digest -ne (Get-FileHash -LiteralPath (Join-Path $root $files[$entry.FullName])).Hash) { throw "Package byte mismatch: $($entry.FullName)" }
    }
} finally { $archive.Dispose() }
$sums = @("$((Get-FileHash -LiteralPath $zip).Hash)  $([IO.Path]::GetFileName($zip))",
    "$((Get-FileHash -LiteralPath (Join-Path $root 'bin/Release/KellysMEDALSPublicUI.dll')).Hash)  BepInEx/plugins/KellysMEDALSPublicUI/KellysMEDALSPublicUI.dll")
$sums | Set-Content -LiteralPath (Join-Path $dist 'MEDALS-UI-0.1.3-SHA256SUMS.txt') -Encoding ascii
Write-Host "Packaged and hash-verified $zip"
$sums | Write-Output
