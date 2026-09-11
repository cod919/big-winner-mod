$ErrorActionPreference = 'Stop'
$gameRoot = $PSScriptRoot
$start = [System.Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $gameRoot 'TheBigWinner.exe'
$start.WorkingDirectory = $gameRoot
$start.UseShellExecute = $false
$start.EnvironmentVariables['DOTNET_STARTUP_HOOKS'] = Join-Path $gameRoot 'BigWinnerMod\release\BigWinnerMod.dll'
[System.Diagnostics.Process]::Start($start) | Out-Null
