# Downloads the SSMap map data from its public GitHub repository into a folder. SaltMap does
# not redistribute SSMap (it has no licence); each install fetches it from the source.
# Dot-source this file, then call:  Get-SsMap -Destination <folder>
$SsMapRepo = 'Kaszub09/SSMap'
# The SSMap version SaltMap was tested with (2025-07-06); its markers.js format is relied on.
$SsMapCommit = '564b1638a0c0a2258d6392c5312ccee5e4f2c67b'

function Get-SsMap([string]$Destination) {
    $url = "https://github.com/$SsMapRepo/archive/$SsMapCommit.zip"
    $download = Join-Path $env:TEMP "SSMap-$SsMapCommit.zip"
    Write-Host "Downloading the SSMap map data (about 120 MB) from github.com/$SsMapRepo ..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
        & curl.exe -L --fail --progress-bar -o $download $url
        if ($LASTEXITCODE) { throw "Downloading $url failed (curl exit code $LASTEXITCODE)." }
    } else {
        (New-Object Net.WebClient).DownloadFile($url, $download)
    }

    # Only what SaltMap reads, plus SSMap's own README for its credits.
    Write-Host "Extracting the map data ..."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $partial = "$Destination.partial"
    if (Test-Path $partial) { Remove-Item $partial -Recurse -Force }
    $zip = [IO.Compression.ZipFile]::OpenRead($download)
    $count = 0
    try {
        foreach ($entry in $zip.Entries) {
            $slash = $entry.FullName.IndexOf('/')          # entries start with "SSMap-<commit>/"
            if ($slash -lt 0 -or $entry.FullName.EndsWith('/')) { continue }
            $rel = $entry.FullName.Substring($slash + 1)
            if (-not ($rel.StartsWith('tiles/') -or $rel.StartsWith('images/markerIcons/') -or $rel -eq 'markers.js' -or $rel -eq 'README.md')) { continue }
            $target = Join-Path $partial ($rel -replace '/', '\')
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            $count++
        }
    } finally {
        $zip.Dispose()
    }
    if (-not (Test-Path (Join-Path $partial 'tiles\0'))) { throw "The download did not contain SSMap's tiles." }

    Set-Content -Path (Join-Path $partial 'SOURCE.txt') -Encoding ASCII -Value @(
        "Downloaded by SaltMap's installer from https://github.com/$SsMapRepo (commit $SsMapCommit).",
        "SSMap is by Kaszub09 and contributors, built from the screenshot map of the world by",
        "u/magicofgames (https://www.reddit.com/r/saltandsanctuary/comments/f0t1sa/).",
        "It has no licence: keep it for personal use."
    )
    if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
    Move-Item $partial $Destination
    Remove-Item $download -Force
    Write-Host "Map data ready in $Destination ($count files)"
}
