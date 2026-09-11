param(
    [Parameter(Mandatory = $true)]
    [string]$GameDirectory
)
$ErrorActionPreference = 'Stop'
$gamePath = (Resolve-Path -LiteralPath $GameDirectory).Path
$output = Join-Path $PSScriptRoot 'artifacts\release'
$package = Join-Path $PSScriptRoot 'artifacts\package'
$zip = Join-Path $PSScriptRoot 'artifacts\BigWinnerMod-v0.3.1-win-x64.zip'
& dotnet build (Join-Path $PSScriptRoot 'src\BigWinnerMod.csproj') -c Release "-p:GameDir=$gamePath" -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
New-Item -ItemType Directory -Force -Path (Join-Path $package 'BigWinnerMod\release') | Out-Null
Copy-Item -LiteralPath (Join-Path $output 'BigWinnerMod.dll'),(Join-Path $output 'BigWinnerMod.deps.json') -Destination (Join-Path $package 'BigWinnerMod\release') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install\Start-Mod.vbs'),(Join-Path $PSScriptRoot 'install\Start-Mod.ps1') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'data\maps.json') -Destination (Join-Path $package 'BigWinnerMod\maps.json') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $package 'BigWinnerMod\README.zh-CN.md') -Force
# Explicit file list prevents old build or game files from entering the archive.
Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($zip, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @('Start-Mod.vbs','Start-Mod.ps1','BigWinnerMod\release\BigWinnerMod.dll','BigWinnerMod\release\BigWinnerMod.deps.json','BigWinnerMod\maps.json','BigWinnerMod\README.zh-CN.md')
    foreach ($relative in $files) {
        $entry = $archive.CreateEntry($relative.Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal)
        $input = [IO.File]::OpenRead((Join-Path $package $relative))
        $destination = $entry.Open()
        try { $input.CopyTo($destination) } finally { $destination.Dispose(); $input.Dispose() }
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Write-Output "Release ZIP: $zip"
