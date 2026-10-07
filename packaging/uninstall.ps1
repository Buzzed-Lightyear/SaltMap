# Removes SaltMap: puts the original salt.exe back and deletes the mod's files from Mods.
# Your SaltMap.ini, the log and any SSMap folder in Mods are left alone.
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
Get-ChildItem -LiteralPath $here -Recurse -File | Unblock-File
. ([IO.Path]::Combine($here, 'Find-GameDir.ps1'))
$game = Find-GameDir $GameDir
if (-not $game) { throw "Salt and Sanctuary not found. Run: uninstall.ps1 -GameDir `"D:\path\to\Salt and Sanctuary`"" }
if (Get-Process salt -ErrorAction SilentlyContinue) { throw "Close Salt and Sanctuary first." }

& ([IO.Path]::Combine($here, 'Patcher', 'SaltPatcher.exe')) $game --restore
if ($LASTEXITCODE) { Write-Warning "Could not restore salt.exe; use Steam's 'Verify integrity of game files' instead." }

$mods = [IO.Path]::Combine($game, 'Mods')
foreach ($f in 'SaltMap.dll', 'SaltMap.pdb', '0Harmony.dll') {
    $p = [IO.Path]::Combine($mods, $f)
    if ([IO.File]::Exists($p)) { Remove-Item -LiteralPath $p -Force }
}
Write-Host "SaltMap removed. The Mods folder and its settings and log were kept."
