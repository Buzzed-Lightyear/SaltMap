SaltMap - minimap and map page for Salt and Sanctuary (Windows, Steam)

Install
  Close the game, then double-click Install.cmd. It finds the game through Steam,
  patches salt.exe once (the original is kept as salt.exe.orig), installs the mod, and on
  the first install downloads the map data (about 120 MB) from the SSMap project on GitHub
  into the game's Mods\SSMap folder. Then start the game.

  Options (run from PowerShell, or add them after Install.cmd):
    install.ps1 -GameDir "D:\...\Salt and Sanctuary"   if the game is not found
    install.ps1 -SsMap "D:\SSMap"                       use a copy of SSMap you already have
    install.ps1 -NoMapDownload                          skip the download

  The log is <game folder>\Mods\SaltMap.log.

Remove
  powershell -ExecutionPolicy Bypass -File uninstall.ps1
  (or Steam's "Verify integrity of game files", then delete the Mods folder).

After a game update, run Install.cmd again: it re-patches the new salt.exe.

Controls, settings and details: see the project README on GitHub.

Map credits: the SSMap interactive map (https://github.com/Kaszub09/SSMap) by Kaszub09 and
contributors, made from the screenshot map of the world by u/magicofgames
(https://www.reddit.com/r/saltandsanctuary/comments/f0t1sa/). SSMap is not part of this
package; the installer downloads it from its own repository.

SaltMap is MIT licensed (LICENSE). Third-party parts in this package: Harmony
(0Harmony.dll, MIT licence, https://github.com/pardeike/Harmony) and Mono.Cecil
(Patcher\Mono.Cecil*.dll, MIT licence, https://github.com/jbevain/cecil).
