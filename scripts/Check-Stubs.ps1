# Builds the mod against the reference stub (Reference\salt), as GitHub Actions does, and
# checks every game type and member it uses against the installed game's salt.exe.
# Run after changing Reference\salt\Stubs.cs or using new game members. Needs the game.
#   .\scripts\Check-Stubs.ps1 [game folder]
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\Find-GameDir.ps1"
$game = Find-GameDir $GameDir
if (-not $game) { throw "Salt and Sanctuary not found; pass its folder." }

$out = Join-Path $root 'obj\stubcheck'
dotnet build "$root\Mod\SaltMap.csproj" -c Release --nologo -v quiet -p:UseGame=false -o "$out\mod"
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet build "$root\tools\StubCheck\StubCheck.csproj" -c Release --nologo -v quiet -o "$out\tool"
if ($LASTEXITCODE) { exit $LASTEXITCODE }

& "$out\tool\StubCheck.exe" "$out\mod\SaltMap.dll" $game
exit $LASTEXITCODE
