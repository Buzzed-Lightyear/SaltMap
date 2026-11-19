using System;
using ProjectTower;

namespace SaltMap
{
    /// <summary>
    /// Works around two crashes of the game's exclusive fullscreen mode (config.ini
    /// fullscreen=1). Neither comes from the mod; see CLAUDE.md, "Crash on exit".
    /// Closing: while the game window is destroyed, MonoGame's OnDeactivate calls
    /// MinimizeFullScreen and DXGI refuses SetFullscreenState (E_ACCESSDENIED). WinForms
    /// then opens its error dialog, whose message loop runs one more game tick after the
    /// game has been torn down, and that tick crashes. Ignoring the refusal while the
    /// window closes lets the game exit normally.
    /// Starting: when DXGI will not enter exclusive fullscreen (DXGI_ERROR_NOT_CURRENTLY_AVAILABLE),
    /// Game1.SetResolution retries and then means to fall back to borderless, but keeps the
    /// exclusive mode by mistake and throws. The mod then sets up borderless itself.
    /// </summary>
    internal static class Fullscreen
    {
        const int Exclusive = 1, Borderless = 2;   // config.ini fullscreen= values

        /// <summary>After WinFormsGameWindow.MinimizeFullScreen: the exception to let through, or null.</summary>
        public static Exception AfterMinimize(object window, Exception e)
        {
            if (e == null || !GameApi.IsWindowClosing(window)) return e;   // during play, as before
            Log.Info("fullscreen: leaving exclusive fullscreen failed as the window closed ("
                + e.GetType().Name + ": " + e.Message.Trim() + "); ignored so the game can exit");
            return null;
        }

        /// <summary>After Game1.SetResolution: the exception to let through, or null.</summary>
        public static Exception AfterSetResolution(Exception e, int width, int height, int fullScreen, bool vsync, int framerate)
        {
            if (e == null || fullScreen != Exclusive) return e;
            Log.Info("fullscreen: the game could not enter exclusive fullscreen ("
                + e.GetType().Name + ": " + e.Message.Trim() + "); setting up borderless instead");
            try
            {
                // The game's own fallback has already set ConfigMgr.fullScreen to borderless,
                // so the options menu shows the mode actually in use.
                Game1.SetResolution(width, height, Borderless, vsync, framerate);
                Log.Info("fullscreen: borderless set up");
                return null;
            }
            catch (Exception again)
            {
                Log.Info("fullscreen: borderless failed too: " + again.Message.Trim());
                return e;
            }
        }
    }
}
