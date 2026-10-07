# Installs SaltMap into Salt and Sanctuary: patches salt.exe once (keeping the original
# as salt.exe.orig), copies the mod into the game's Mods folder, and downloads the SSMap
# map data from its GitHub repository into Mods\SSMap if no copy is set up yet.
#   install.ps1                                   find the game through Steam
#   install.ps1 -GameDir "D:\...\Salt and Sanctuary"
#   install.ps1 -SsMap "D:\SSMap"                 use an existing SSMap copy instead
#   install.ps1 -NoMapDownload                    never download the map data
# Paths use -LiteralPath: plain -Path reads [ ] in folder names as wildcards.
param([string]$GameDir, [string]$SsMap, [switch]$NoMapDownload)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
# Files from a downloaded zip carry Windows' "from the internet" mark, and .NET will
# not load a marked mod DLL. Clear it on everything in this package.
Get-ChildItem -LiteralPath $here -Recurse -File | Unblock-File

. ([IO.Path]::Combine($here, 'Find-GameDir.ps1'))
$game = Find-GameDir $GameDir
if (-not $game) { throw "Salt and Sanctuary not found. Run: install.ps1 -GameDir `"D:\path\to\Salt and Sanctuary`"" }
if (Get-Process salt -ErrorAction SilentlyContinue) { throw "Close Salt and Sanctuary first." }
Write-Host "Game folder: $game"

& ([IO.Path]::Combine($here, 'Patcher', 'SaltPatcher.exe')) $game
if ($LASTEXITCODE) { throw "The patcher failed (exit code $LASTEXITCODE); see the message above." }

$mods = [IO.Path]::Combine($game, 'Mods')
[void][IO.Directory]::CreateDirectory($mods)
Get-ChildItem -LiteralPath ([IO.Path]::Combine($here, 'Mods')) -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination ([IO.Path]::Combine($mods, $_.Name)) -Force
}
Get-ChildItem -LiteralPath $mods -File -Filter *.dll | Unblock-File
Write-Host "Mod installed to $mods"

# The map pictures come from a local copy of the SSMap web map (see README).
$ini = [IO.Path]::Combine($mods, 'SaltMap.ini')
if ($SsMap) {
    $lines = @()
    if (Test-Path -LiteralPath $ini) { $lines = @(Get-Content -LiteralPath $ini | Where-Object { $_ -notmatch '^\s*ssmap\s*=' }) }
    else { $lines = @('# SaltMap settings. Lines are key=value; # starts a comment.') }
    $lines += "ssmap=$SsMap"
    Set-Content -LiteralPath $ini -Value $lines -Encoding ASCII
    Write-Host "SSMap folder set to $SsMap"
}
$ssmapDir = [IO.Path]::Combine($mods, 'SSMap')
if (Test-Path -LiteralPath $ini) {
    $line = Get-Content -LiteralPath $ini | Where-Object { $_ -match '^\s*ssmap\s*=' } | Select-Object -Last 1
    if ($line) {
        $v = ($line -split '=', 2)[1].Trim()
        $ssmapDir = if ([IO.Path]::IsPathRooted($v)) { $v } else { [IO.Path]::Combine($mods, $v) }
    }
}
if ([IO.Directory]::Exists([IO.Path]::Combine($ssmapDir, 'tiles', '0'))) {
    Write-Host "Map data found in $ssmapDir"
} elseif ($NoMapDownload) {
    Write-Warning "No SSMap map data in $ssmapDir, and -NoMapDownload was given: the maps will be empty until it is there."
} else {
    . ([IO.Path]::Combine($here, 'Get-SsMap.ps1'))
    try { Get-SsMap -Destination $ssmapDir }
    catch { Write-Warning "$($_.Exception.Message) Run the installer again, or put a copy of https://github.com/Kaszub09/SSMap in $ssmapDir." }
}
Write-Host "Done. Start the game; the log is $mods\SaltMap.log"
