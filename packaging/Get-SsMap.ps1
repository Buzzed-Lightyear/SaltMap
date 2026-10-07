# Downloads the SSMap map data from its public GitHub repository into a folder. SaltMap does
# not redistribute SSMap (it has no licence); each install fetches it from the source.
# Dot-source this file, then call:  Get-SsMap -Destination <folder>
# Paths use -LiteralPath throughout: with plain -Path, PowerShell reads [ ] in a folder or
# user name as wildcards and then cannot find files that exist.
$SsMapRepo = 'Kaszub09/SSMap'
# The SSMap version SaltMap was tested with (2025-07-06); its markers.js format is relied on.
$SsMapCommit = '564b1638a0c0a2258d6392c5312ccee5e4f2c67b'

function Get-SsMap([string]$Destination) {
    $url = "https://github.com/$SsMapRepo/archive/$SsMapCommit.zip"
    $download = Join-Path ([IO.Path]::GetTempPath()) "SSMap-$SsMapCommit.zip"

    Write-Host "Downloading the SSMap map data (about 120 MB) from github.com/$SsMapRepo ..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    try {
        if (Get-Command curl.exe -ErrorAction SilentlyContinue) {
            & curl.exe -L --fail --progress-bar -o $download $url
            if ($LASTEXITCODE) { throw "curl exit code $LASTEXITCODE" }
        } else {
            (New-Object Net.WebClient).DownloadFile($url, $download)
        }
    } catch {
        throw "Download of $url failed: $($_.Exception.Message)"
    }

    # Only what SaltMap reads, plus SSMap's own README for its credits. The many small tile
    # files are what make this slow, especially while antivirus software scans each one.
    $partial = "$Destination.partial"
    try {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Recurse -Force }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead($download)
        $count = 0
        try {
            $total = $zip.Entries.Count
            $seen = 0
            foreach ($entry in $zip.Entries) {
                $seen++
                if ($seen % 500 -eq 0) {
                    Write-Progress -Activity 'Extracting the SSMap map data' -Status "$count files" -PercentComplete (100 * $seen / $total)
                }
                $slash = $entry.FullName.IndexOf('/')      # entries start with "SSMap-<commit>/"
                if ($slash -lt 0 -or $entry.FullName.EndsWith('/')) { continue }
                $rel = $entry.FullName.Substring($slash + 1)
                if (-not ($rel.StartsWith('tiles/') -or $rel.StartsWith('images/markerIcons/') -or $rel -eq 'markers.js' -or $rel -eq 'README.md')) { continue }
                $target = [IO.Path]::Combine($partial, $rel.Replace('/', '\'))
                [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
                $count++
            }
        } finally {
            $zip.Dispose()
            Write-Progress -Activity 'Extracting the SSMap map data' -Completed
        }
        if (-not [IO.Directory]::Exists([IO.Path]::Combine($partial, 'tiles', '0'))) { throw "The download did not contain SSMap's tiles." }

        [IO.File]::WriteAllLines([IO.Path]::Combine($partial, 'SOURCE.txt'), [string[]]@(
            "Downloaded by SaltMap's installer from https://github.com/$SsMapRepo (commit $SsMapCommit).",
            "SSMap is by Kaszub09 and contributors, built from the screenshot map of the world by",
            "u/magicofgames (https://www.reddit.com/r/saltandsanctuary/comments/f0t1sa/).",
            "It has no licence: keep it for personal use."))
        if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination -Recurse -Force }
        Move-Item -LiteralPath $partial -Destination $Destination
    } catch {
        throw "Extracting the map data into $Destination failed: $($_.Exception.Message)"
    }
    Write-Host "Map data ready in $Destination ($count files)"

    # Tidying up the temporary download must never fail the install.
    Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue
}
