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
        $vdf = [IO.Path]::Combine($steam, 'steamapps', 'libraryfolders.vdf')
        if ([IO.File]::Exists($vdf)) {
            foreach ($m in (Select-String -LiteralPath $vdf -Pattern '"path"\s+"([^"]+)"')) {
                $libraries += ($m.Matches[0].Groups[1].Value -replace '\\\\', '\')
            }
        }
        foreach ($lib in $libraries) { $candidates += [IO.Path]::Combine($lib, 'steamapps', 'common', 'Salt and Sanctuary') }
    }
    $candidates += 'C:\Program Files (x86)\Steam\steamapps\common\Salt and Sanctuary'

    # .NET file checks, not Test-Path: Test-Path reads [ ] in a folder name as wildcards.
    foreach ($c in $candidates) {
        if ($c -and [IO.File]::Exists([IO.Path]::Combine($c, 'salt.exe'))) {
            return [IO.Path]::GetFullPath($c).TrimEnd('\')
        }
    }
    return $null
}
