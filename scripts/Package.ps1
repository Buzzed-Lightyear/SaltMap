# Builds the release package dist\SaltMap-<version>.zip: the patcher, the mod and the
# install scripts. Used by GitHub Actions (.github/workflows/release.yml); needs no game:
# the mod is built against the reference stub in Reference\salt.
#   .\scripts\Package.ps1 [-NoZip]
param([switch]$NoZip)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$proj = Get-Content "$root\Mod\SaltMap.csproj"
$version = @($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { throw "No <Version> in Mod\SaltMap.csproj" }

# A release tag must match the version the mod reports in its log.
$tag = $env:GITHUB_REF_NAME
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $tag -ne "v$version") {
    throw "Tag $tag does not match the mod version $version (expected v$version). Update <Version> in Mod\SaltMap.csproj."
}

$name = "SaltMap-$version"
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist $name
$build = Join-Path $root 'obj\package'
foreach ($d in $stage, $build) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }

dotnet build "$root\Patcher\SaltPatcher.csproj" -c Release --nologo -v quiet -o "$build\patcher"
if ($LASTEXITCODE) { throw "Patcher build failed" }
dotnet build "$root\Mod\SaltMap.csproj" -c Release --nologo -v quiet -p:UseGame=false -o "$build\mod"
if ($LASTEXITCODE) { throw "Mod build failed" }

New-Item -ItemType Directory -Force -Path "$stage\Patcher", "$stage\Mods" | Out-Null
Copy-Item "$build\patcher\SaltPatcher.exe", "$build\patcher\Mono.Cecil*.dll" "$stage\Patcher"
if (Test-Path "$build\patcher\SaltPatcher.exe.config") { Copy-Item "$build\patcher\SaltPatcher.exe.config" "$stage\Patcher" }
# Only the mod and Harmony: never the stub salt.dll that sits in the same build folder.
Copy-Item "$build\mod\SaltMap.dll", "$build\mod\0Harmony.dll" "$stage\Mods"
Copy-Item "$root\packaging\*" $stage
Copy-Item "$root\scripts\Find-GameDir.ps1", "$root\LICENSE" $stage

if ($NoZip) { Write-Host "Staged $stage"; exit 0 }
$zip = Join-Path $dist "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Write-Host "Packaged $zip"
