# Patches salt.exe if it is not patched yet, then builds the mod into the game's Mods folder.
# Usage:  .\build.ps1                       (finds the game through Steam, or uses SALT_DIR)
#         .\build.ps1 "D:\Games\Salt and Sanctuary"
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\scripts\Find-GameDir.ps1"
$found = Find-GameDir $GameDir
if (-not $found) {
    Write-Error "Salt and Sanctuary not found. Pass its folder: .\build.ps1 `"D:\path\to\Salt and Sanctuary`" (or set SALT_DIR)."
}
$env:SALT_DIR = $found
Write-Host "Game folder: $found"

dotnet build "$PSScriptRoot\Patcher\SaltPatcher.csproj" -c Release --nologo -v quiet
if ($LASTEXITCODE) { exit $LASTEXITCODE }

& "$PSScriptRoot\Patcher\bin\Release\net472\SaltPatcher.exe" $found
if ($LASTEXITCODE) { exit $LASTEXITCODE }

dotnet build "$PSScriptRoot\Mod\SaltMap.csproj" -c Release --nologo -v quiet
exit $LASTEXITCODE
