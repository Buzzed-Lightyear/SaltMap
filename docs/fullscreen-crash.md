# The exclusive fullscreen crashes

Salt and Sanctuary has three display modes in its video options: windowed, fullscreen and
borderless. In exclusive fullscreen the game can crash in two places. Both are in the game
and in MonoGame (the framework it is built on), not in SaltMap: players have reported the
crash on quit on the game's Steam bug board since April 2025
([thread](https://steamcommunity.com/app/283640/discussions/2/592895054280977074/)), with
the same error as below. No developer reply or fix has been posted there.

**Workaround without the mod:** set the video option *Fullscreen* to *Borderless*.
**With SaltMap 0.4.5 or later:** both crashes are worked around (`Mod/Fullscreen.cs`).

## Crash on quit

Symptom: quitting from play or from the title screen ends with the game's "Sorry, an
error occurred" box, showing a `NullReferenceException` in
`MonoGame.Framework.WinFormsGameWindow.TickOnIdle`.

That exception is a follow-on. The chain:

1. Quitting disposes MonoGame's game window. As Windows destroys it, the window loses
   focus and MonoGame's `OnDeactivate` calls `MinimizeFullScreen`, which asks DXGI to
   leave exclusive fullscreen (`SwapChain.SetFullscreenState(false)`).
2. DXGI refuses: `SharpDXException` `E_ACCESSDENIED` (0x80070005). The exception is
   thrown inside a window message.
3. WinForms catches it and opens its "Unhandled exception" dialog. That dialog's message
   loop keeps raising `Application.Idle`, so MonoGame's `TickOnIdle` runs the game again.
   `Game.Tick` does not clear the exit flag, so the extra tick disposes the window a
   second time and sets `Game` to null.
4. The next idle tick calls `Game.Tick()` on null. That exception escapes, the game's
   handler shows the error box, and the process ends. The first exception is lost.

SaltMap adds a Harmony finalizer to `WinFormsGameWindow.MinimizeFullScreen`: if it throws
while the window is being destroyed (`Form.Disposing`), the exception is logged and
dropped, so the quit finishes normally. During play the method is left alone.

## Crash at launch

Symptom: after about 20 seconds of black screen at launch, the same error box with a
`SharpDXException` `DXGI_ERROR_NOT_CURRENTLY_AVAILABLE` (0x887A0022).

DXGI will not hand over the screen, usually because the game window is not in front. The
game's `Game1.SetResolution` retries for 5 seconds and then means to fall back to
borderless, but it keeps the exclusive mode by mistake, retries another 5 seconds and
throws.

SaltMap adds a Harmony finalizer to `Game1.SetResolution`: if exclusive fullscreen fails,
it sets up borderless itself. The game's own fallback has already switched the setting to
borderless (in memory), so the options menu shows the mode in use.

## How it was found and tested

Logged in the running game on 2026-11-19, with every thrown exception recorded:
exclusive fullscreen crashed on every quit tried (from play and from the title screen),
borderless never did, and 3 of 7 launches failed as above. The stacks matched the Steam
report.

With 0.4.5, quitting in exclusive fullscreen (from play, from the title screen, and by
closing the window) exits normally. The launch fallback has not been seen in action yet:
it only runs when DXGI refuses the screen at launch.
