# Setup

## What the installer does

`Install.cmd` runs `install.ps1`, which:

1. finds the game through Steam (`libraryfolders.vdf`);
2. patches `salt.exe` once, keeping the original as `salt.exe.orig` (see
   [development.md](development.md#how-it-works));
3. copies `SaltMap.dll` and `0Harmony.dll` into the game's `Mods` folder;
4. on the first install, downloads the map data (about 120 MB) into `Mods\SSMap`.

After a Steam update of the game, run it again: it patches the new `salt.exe`.

## Installer options

Add them after `Install.cmd` in a command prompt, or pass them to `install.ps1` in
PowerShell:

```powershell
Install.cmd -GameDir "D:\Games\Salt and Sanctuary"   # if the game is not found
Install.cmd -SsMap "D:\SSMap"                        # use a copy of SSMap you already have
Install.cmd -NoMapDownload                           # skip the download
```

## Uninstall

Right-click `uninstall.ps1` and choose *Run with PowerShell*, or from a prompt:

```powershell
powershell -ExecutionPolicy Bypass -File uninstall.ps1
powershell -ExecutionPolicy Bypass -File uninstall.ps1 -GameDir "D:\Games\Salt and Sanctuary"
```

It puts the original `salt.exe` back and removes `SaltMap.dll` and `0Harmony.dll`. Your
`SaltMap.ini`, the log and the map data in `Mods\SSMap` stay; delete the `Mods` folder to
remove them too. Steam's *Verify integrity of game files* also restores `salt.exe`.

## Settings

`Mods\SaltMap.ini` is created on first start. Everything except `ssmap` can also be
changed in game.

| Key | Meaning |
| --- | --- |
| `ssmap` | Folder of the SSMap copy; a relative path counts from `Mods`. Default `SSMap`. |
| `minimap` | `on` or `off`. |
| `minimapZoom` | 0 (far) to 7 (near). |
| `mapFilter` | What the map page shows, 0 (all) to 6 (map only). |
| `guide` | `all`, `main` or `off`. |

## Map data

SaltMap draws the [SSMap](https://kaszub09.github.io/SSMap/) interactive map, made by
Kaszub09 and contributors from the screenshot map of the whole world assembled by
[u/magicofgames](https://www.reddit.com/r/saltandsanctuary/comments/f0t1sa/ultimate_map_still_wip_well_no_but_actually_yes/).
Its tiles, marker list and icons are read from a local copy at run time and are not part
of this repository or its releases: SSMap has no licence, so SaltMap does not
redistribute it.

The installer fetches it from [github.com/Kaszub09/SSMap](https://github.com/Kaszub09/SSMap),
pinned to the commit SaltMap was tested with, and keeps `tiles\`, `markers.js`,
`images\markerIcons\` and `README.md` in `Mods\SSMap`, with a `SOURCE.txt` saying where
they came from. To use a copy elsewhere, set `ssmap=` or install with `-SsMap`.

## Logs and diagnostics

All in the game's `Mods` folder:

| File | Written | Contents |
| --- | --- | --- |
| `SaltMap.log` | every launch (replaced) | what loaded, hooks, errors |
| `marker-links.tsv` | every launch | which map icon is tied to which object in the level |
| `marker-status.tsv`, `save-flags.txt`, `guide-status.txt` | when the map page opens | each icon's state, the save's progress flags, the guide's steps |
| `entities.tsv` | at the title screen, with `dumpEntities=on` | every object in the level |

If the mod cannot load at all, the game still starts and writes
`SaltMap-loader-error.log` next to `salt.exe`.

## Network

The mod sends the player's position to UDP port 47800 on this computer (loopback only),
for an optional live web-map tracker. Nothing leaves the machine.
