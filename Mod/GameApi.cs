using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectTower.hud;
using ProjectTower.player;

namespace SaltMap
{
    /// <summary>
    /// Game and MonoGame members the mod needs that are not public. .NET Framework
    /// enforces access across assemblies, so these go through Harmony's accessors.
    /// Each lookup fails on its own (logged once) without taking the others down.
    /// </summary>
    internal static class GameApi
    {
        static readonly AccessTools.FieldRef<PlayerDraw, float> hudScale =
            Try("PlayerDraw.scale", () => AccessTools.FieldRefAccess<PlayerDraw, float>("scale"));

        static readonly AccessTools.FieldRef<SpriteBatch, bool> beginCalled =
            Try("SpriteBatch._beginCalled", () => AccessTools.FieldRefAccess<SpriteBatch, bool>("_beginCalled"));

        static readonly Action<Vector2, int, float, float> drawArrow =
            Try("InterfaceRender.DrawArrow", () => AccessTools.MethodDelegate<Action<Vector2, int, float, float>>(
                AccessTools.Method(typeof(InterfaceRender), "DrawArrow",
                    new[] { typeof(Vector2), typeof(int), typeof(float), typeof(float) })));

        static readonly Func<Player, bool> mouseKeyboardActive =
            Try("InputMgr.IsMouseKeyboardActive", () => AccessTools.MethodDelegate<Func<Player, bool>>(
                AccessTools.Method(AccessTools.TypeByName("ProjectTower.config.InputMgr"), "IsMouseKeyboardActive", new[] { typeof(Player) })));

        static readonly Action playSelect = SoundAction("PlaySelect");
        static readonly Action playAccept = SoundAction("PlayAccept");
        static readonly Action playCancel = SoundAction("PlayCancel");

        static T Try<T>(string what, Func<T> make) where T : class
        {
            try
            {
                T made = make();
                if (made == null) Log.Info("GameApi: " + what + " not found");
                return made;
            }
            catch (Exception e)
            {
                Log.Info("GameApi: " + what + " unavailable: " + e.Message);
                return null;
            }
        }

        static Action SoundAction(string name)
        {
            return Try("Sound." + name, () => AccessTools.MethodDelegate<Action>(
                AccessTools.Method(AccessTools.TypeByName("ProjectTower.audio.Sound"), name, Type.EmptyTypes)));
        }

        /// <summary>The HUD's scale (PlayerDraw.scale): 2 on screens wider than 2560, times the HUD size option.</summary>
        public static float HudScale(Player p)
        {
            if (p == null || p.draw == null || hudScale == null) return 1f;
            float s = hudScale(p.draw);
            return s > 0.1f ? s : 1f;
        }

        /// <summary>True while the batch is between Begin and End. False if that cannot be read.</summary>
        public static bool IsBegun(SpriteBatch batch)
        {
            return batch != null && beginCalled != null && beginCalled(batch);
        }

        /// <summary>Whether IsBegun can be trusted.</summary>
        public static bool CanTellBegun { get { return beginCalled != null; } }

        public static void DrawArrow(Vector2 at, int arrow, float alpha, float scale)
        {
            if (drawArrow != null) drawArrow(at, arrow, alpha, scale);
        }

        /// <summary>
        /// True when the player last used keyboard or mouse, false after controller
        /// input: the test the game uses to choose its button prompts.
        /// </summary>
        public static bool UsingKeyboard(Player p)
        {
            if (p == null || mouseKeyboardActive == null) return true;
            return mouseKeyboardActive(p);
        }

        /// <summary>The controller this player uses (Player.gamepadIdx, set by whichever pad pressed Start).</summary>
        public static GamePadState Pad(Player p)
        {
            int idx = p == null ? 0 : Math.Max(0, Math.Min(3, p.gamepadIdx));
            return GamePad.GetState((PlayerIndex)idx);
        }

        public static void PlaySelect() { if (playSelect != null) playSelect(); }
        public static void PlayAccept() { if (playAccept != null) playAccept(); }
        public static void PlayCancel() { if (playCancel != null) playCancel(); }
    }
}
