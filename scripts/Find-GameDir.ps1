# Finds the Salt and Sanctuary folder: the given path, else SALT_DIR, else every
# Steam library listed in Steam's libraryfolders.vdf. Returns $null if none has salt.exe.
# Dot-source this file, then call:  Find-GameDir [-Hint <folder>]
function Find-GameDir([string]$Hint) {
    $candidates = @()
    if ($Hint) { $candidates += $Hint }
    if ($env:SALT_DIR) { $candidates += $env:SALT_DIR }

    $steam = $null
    $key = Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if ($key -and $key.SteamPath) { $steam = $key.SteamPath }
    if (-not $steam) {
        $key = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -ErrorAction SilentlyContinue
        if ($key -and $key.InstallPath) { $steam = $key.InstallPath }
    }
    if ($steam) {
        $libraries = @($steam)
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"')) {
                $libraries += ($m.Matches[0].Groups[1].Value -replace '\\\\', '\')
            }
        }
        foreach ($lib in $libraries) { $candidates += (Join-Path $lib 'steamapps\common\Salt and Sanctuary') }
    }
    $candidates += 'C:\Program Files (x86)\Steam\steamapps\common\Salt and Sanctuary'

    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'salt.exe'))) { return (Resolve-Path $c).Path.TrimEnd('\') }
    }
    return $null
}
