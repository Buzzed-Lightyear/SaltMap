# Development

## How it works

- `Patcher/` builds `SaltPatcher.exe`, which adds one guarded call to the start of
  `Game1.Initialize` in `salt.exe` that loads `Mods\SaltMap.dll`. If the mod is missing or
  fails, the game starts anyway and writes `SaltMap-loader-error.log` in the game folder.
- `Mod/` builds `SaltMap.dll`, which hooks the game at run time with
  [Harmony](https://github.com/pardeike/Harmony). Nothing else in the game is changed.
  Every hook is in `Mod/Hooks.cs`, each a thin `try`/`catch` around mod code, so a bug in
  the mod cannot take the game down.
- The overlay draws from a prefix on `HUD.DrawModalDialog`: above the HUD, below modal
  dialogs and the cursor. Why there: [rendering.md](rendering.md).
- Game positions are matched to SSMap's map by fitting the level's collision geometry to
  the map pictures. Icons are tied to the chests, bags, bosses and NPCs they stand for, so
  the save's progress flags tell when they are done. Details: [minimap.md](minimap.md).
- `Mod/Fullscreen.cs` works around two crashes of the game's exclusive fullscreen mode:
  [fullscreen-crash.md](fullscreen-crash.md).

## Building from source

Needs Windows, the [.NET SDK](https://dotnet.microsoft.com/download) and the game.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1            # finds the game through Steam
powershell -ExecutionPolicy Bypass -File build.ps1 "D:\Games\Salt and Sanctuary"
```

This patches the game once, then builds and installs the mod; close the game first. The
mod cannot be reloaded while the game runs, so every change needs a restart. Its log is
`Mods\SaltMap.log`.

## The reference stub

With the game installed, the mod compiles against the real `salt.exe`. Without it (as on
GitHub Actions) it compiles against `Reference/salt`, a stub that declares only the game
members the mod uses (names and signatures, no game code), and MonoGame's NuGet package.

After using new game members, add them to the stub and run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Check-Stubs.ps1
```

It checks every reference and constant in the stub-built DLL against the installed game.

## Releasing

1. Set `<Version>` in `Mod/SaltMap.csproj`.
2. Add a `## <version>` section to [CHANGELOG.md](../CHANGELOG.md); it becomes the release
   notes, and the release fails without it.
3. Push a tag `v<version>`. The [release workflow](../.github/workflows/release.yml) builds
   `SaltMap-<version>.zip` (with `scripts/Package.ps1`) and attaches it to a GitHub release.

`scripts/Package.ps1 -NoZip` stages the same package locally in `dist/`.
