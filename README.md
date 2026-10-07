# SaltMap

A minimap and a full map page for **Salt and Sanctuary** (Steam, Windows).

- **Minimap** in the top-right corner, tucked into the HUD's corner ornament, following
  your character, with zoom levels.
- **Map page** in the Escape menu (a new map icon after the bag, skull and gear): pan,
  zoom, centre on yourself, and see the name of any icon.
- **Icons** for items, NPCs, bosses and sanctuaries. Item and boss icons **disappear once
  you have collected the item or beaten the boss**, read from your save's own progress.
  Sanctuaries and NPCs always stay.
- **Enemies** as red dots: nearby ones bright, far-away ones dimmer, bosses larger.
- Keyboard, mouse and **controller**, using buttons the game leaves free.

The map pictures and icon positions come from the community **SSMap** web map; the
installer downloads it from its own repository (see [Map data](#map-data)).

## Install

Download `SaltMap-<version>.zip` from the [Releases](../../releases) page, extract it,
close the game and double-click **`Install.cmd`**. That's it: start the game.

The installer finds the game through Steam, patches `salt.exe` once (the original is kept
as `salt.exe.orig`), copies the mod into the game's `Mods` folder, and on the first
install downloads the map data (about 120 MB) into `Mods\SSMap`. Options, from PowerShell
or after `Install.cmd`:

```powershell
install.ps1 -GameDir "D:\Games\Salt and Sanctuary"   # if the game is not found
install.ps1 -SsMap "D:\SSMap"                          # use a copy of SSMap you already have
install.ps1 -NoMapDownload                             # skip the download
```

The mod writes `Mods\SaltMap.log` in the game folder. After a game update, run
`Install.cmd` again: it patches the new `salt.exe`.

**Uninstall:** run `uninstall.ps1` from the same zip (it puts the original `salt.exe`
back), or use Steam's "Verify integrity of game files" and delete the `Mods` folder.

## Map data

SaltMap draws the [SSMap](https://kaszub09.github.io/SSMap/) interactive map, made by
**Kaszub09** and contributors from the screenshot map of the whole world assembled by
**[u/magicofgames](https://www.reddit.com/r/saltandsanctuary/comments/f0t1sa/ultimate_map_still_wip_well_no_but_actually_yes/)**.
Its tiles, marker list and icons are read from a local copy at run time and are not part
of this repository or its releases: SSMap has no licence, so SaltMap does not redistribute
it. The installer fetches it straight from
[github.com/Kaszub09/SSMap](https://github.com/Kaszub09/SSMap), pinned to the version
SaltMap was tested with, and keeps `tiles\`, `markers.js` and `images\markerIcons\` in
`Mods\SSMap` (with a `SOURCE.txt` saying where it came from). To use a copy elsewhere,
set `ssmap=` in `Mods\SaltMap.ini` or pass `-SsMap`. Keep it for personal use.

## Controls

| | Keyboard / mouse | Controller |
|---|---|---|
| Show / hide the minimap | M | D-pad Up (steps near, middle, far, hidden) |
| Zoom the minimap | `=` and `-` | D-pad Up |
| Open the map page | Esc, then the map icon | Back / Select (in play or in the Esc menu) |
| Pan | Arrows / WASD / drag | Left stick, D-pad |
| Zoom | Q E, mouse wheel | Right stick (smooth); LB RB, LT RT |
| Centre on yourself | Space | A |
| Back to the menu | Esc | B |
| Close the menu | | Back / Select |

## Settings

`Mods\SaltMap.ini` is created on first start:

| Key | Meaning |
|---|---|
| `ssmap` | Folder of the SSMap copy; relative paths count from `Mods`. Default `SSMap`. |
| `minimap` | `on` or `off` (also toggled in game). |
| `minimapZoom` | 0 (far) to 7 (near) (also changed in game). |

## How it works

- `Patcher/` builds `SaltPatcher.exe`, which adds one guarded call to the start of
  `Game1.Initialize` in `salt.exe` that loads `Mods\SaltMap.dll`. If the mod is missing or
  fails, the game starts anyway and writes `SaltMap-loader-error.log` in the game folder.
- `Mod/` builds `SaltMap.dll`, which hooks the game at run time with
  [Harmony](https://github.com/pardeike/Harmony). Nothing else in the game is changed.
- Game positions are matched to SSMap's map by fitting the level's collision geometry to
  the map pictures; icons are tied to the chests, bags and bosses they stand for to know
  when they are done. Details in [docs/minimap.md](docs/minimap.md) and
  [docs/rendering.md](docs/rendering.md).
- The mod also sends the player position to UDP port 47800 on this computer, for an
  optional live web-map tracker. Nothing leaves the machine.

## Building from source

Needs Windows, the [.NET SDK](https://dotnet.microsoft.com/download) and the game.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1            # finds the game through Steam
powershell -ExecutionPolicy Bypass -File build.ps1 "D:\Games\Salt and Sanctuary"
```

This patches the game once and builds and installs the mod; close the game first. With the
game installed the mod compiles against the real `salt.exe`. Without it (as on GitHub
Actions) it compiles against `Reference/salt`, a stub that declares only the game members
the mod uses (names and signatures, no game code), and MonoGame's NuGet package.
After using new game members, update the stub and run `scripts\Check-Stubs.ps1`, which
checks every reference and constant against the installed game.

Releases: push a tag `v<version>` matching `<Version>` in `Mod/SaltMap.csproj`; the
[release workflow](.github/workflows/release.yml) builds the zip and attaches it.

## Credits

- [SSMap](https://github.com/Kaszub09/SSMap) by Kaszub09 and contributors: map tiles,
  markers and icons.
- The original screenshot map of the world by
  [u/magicofgames](https://www.reddit.com/r/saltandsanctuary/comments/f0t1sa/ultimate_map_still_wip_well_no_but_actually_yes/).
- [Harmony](https://github.com/pardeike/Harmony) (MIT) and
  [Mono.Cecil](https://github.com/jbevain/cecil) (MIT), included in the release zip.
- [MonoGame](https://github.com/MonoGame/MonoGame) (MIT), used by the game.

SaltMap is a fan project, not affiliated with Ska Studios. It contains no game files.

## License

SaltMap's own code is [MIT licensed](LICENSE). The SSMap map data it downloads is not
covered by this licence.
