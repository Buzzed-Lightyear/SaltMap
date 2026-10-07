using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectTower;
using ProjectTower.config;
using ProjectTower.gamestate;
using ProjectTower.hud;
using ProjectTower.player;

namespace SaltMap
{
    /// <summary>
    /// The minimap: a square of the map in the top-right corner, tucked inside the
    /// game's corner trim, centred on the player. M shows or hides it; = and - (or
    /// numpad + and -) zoom. Both are remembered in SaltMap.ini.
    /// </summary>
    internal static class Minimap
    {
        // Screen pixels per world unit at HUD scale 1, from far to near.
        static readonly float[] Zooms = { 0.012f, 0.018f, 0.026f, 0.036f, 0.05f, 0.07f, 0.1f, 0.14f };
        const int DefaultZoom = 4;

        // Layout in HUD-scale units, measured from PlayerDraw.DrawBackgroundTrim: the
        // top-right trim's corner sits 60 in from the top and right edges.
        const float TrimInset = 60f;
        const float GapInsideTrim = 9f;      // clears the trim's two main strokes
        const float Size = 200f;
        const float IconSize = 15f;
        const float PlayerSize = 6f;
        const float DotSize = 5f;
        static readonly Rectangle TrimSource = new Rectangle(128, 128, 448, 384);

        static readonly Color Edge = new Color(0, 0, 0, 200);

        // D-pad Up steps through these with a controller, then hides the minimap.
        static readonly int[] PadSteps = { 6, 4, 2 };

        static int zoom;
        static bool visible;
        static KeyboardState lastKeys;
        static GamePadState lastPad;
        static bool reported;

        public static void Init()
        {
            zoom = Math.Max(0, Math.Min(Zooms.Length - 1, Settings.GetInt("minimapZoom", DefaultZoom)));
            visible = Settings.GetBool("minimap", true);
        }

        /// <summary>
        /// Reads the minimap and map buttons. Called every rendered frame from the Update hook.
        /// Keyboard: M shows or hides, = and - zoom. Controller (buttons the game leaves
        /// free in the field): Back opens the map page of the Escape menu, D-pad Up steps
        /// the minimap near, middle, far, hidden.
        /// </summary>
        public static void HandleInput()
        {
            KeyboardState keys = Keyboard.GetState();
            KeyboardState before = lastKeys;
            lastKeys = keys;
            Player player = PlayerTracker.MainPlayer;
            GamePadState pad = GameApi.Pad(player);
            GamePadState padBefore = lastPad;
            lastPad = pad;

            // Only in play and with the game focused.
            if (GameStateManager.gameState != 1 || Game1.game == null || !Game1.game.IsActive || player == null) return;

            bool menu = player.playerInv != null && player.playerInv.active;
            bool talking = player.dialog != null && player.dialog.active;
            if (pad.Buttons.Back == ButtonState.Pressed && padBefore.Buttons.Back != ButtonState.Pressed && !talking)
                MenuMap.RequestFromBackButton();
            if (menu || talking || MenuMap.IsOpen) return;

            if (pad.DPad.Up == ButtonState.Pressed && padBefore.DPad.Up != ButtonState.Pressed) StepForPad();

            if (Pressed(keys, before, Keys.M)) SetVisible(!visible);
            int step = 0;
            if (Pressed(keys, before, Keys.OemPlus) || Pressed(keys, before, Keys.Add)) step = 1;
            if (Pressed(keys, before, Keys.OemMinus) || Pressed(keys, before, Keys.Subtract)) step = -1;
            SetZoom(zoom + step);
        }

        static void StepForPad()
        {
            if (!visible) { SetVisible(true); SetZoom(PadSteps[0]); return; }
            foreach (int z in PadSteps)
                if (z < zoom) { SetZoom(z); return; }
            SetVisible(false);
        }

        static void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            Settings.Set("minimap", visible ? "on" : "off");
            Log.Info("minimap " + (visible ? "shown" : "hidden"));
        }

        static void SetZoom(int z)
        {
            z = Math.Max(0, Math.Min(Zooms.Length - 1, z));
            if (z == zoom) return;
            zoom = z;
            Settings.Set("minimapZoom", zoom.ToString(CultureInfo.InvariantCulture));
            Log.Info("minimap zoom " + zoom);
        }

        static bool Pressed(KeyboardState now, KeyboardState before, Keys key)
        {
            return now.IsKeyDown(key) && !before.IsKeyDown(key);
        }

        public static void Draw(GraphicsDevice device, SpriteBatch batch, Texture2D pixel)
        {
            if (!visible || GameStateManager.gameState != 1 || !PlayerTracker.HasPosition || !TileCache.Available) return;
            Player player = PlayerTracker.MainPlayer;
            if (player == null) return;
            // Out of the way while the inventory or a conversation fills the screen.
            if ((player.playerInv != null && player.playerInv.active) || (player.dialog != null && player.dialog.active)) return;

            Viewport view = device.Viewport;
            float s = GameApi.HudScale(player);
            Vector2 trim = new Vector2(view.Width - TrimInset * s, TrimInset * s);
            int size = (int)Math.Round(Size * s);
            int gap = (int)Math.Round(GapInsideTrim * s);
            Rectangle frame = new Rectangle((int)trim.X - gap - size, (int)trim.Y + gap, size, size);

            Vector2 focus = MapSpace.FromWorld(PlayerTracker.Position);
            float scale = Zooms[zoom] * s / MapSpace.PixelsPerUnit;   // screen px per map px

            Rectangle edge = frame;
            edge.Inflate(Math.Max(1, (int)Math.Round(1.5f * s)), Math.Max(1, (int)Math.Round(1.5f * s)));
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, DepthStencilState.None, RasterizerState.CullNone);
            batch.Draw(pixel, edge, Edge);
            batch.End();

            MapView.Draw(device, batch, pixel, frame, focus, scale, IconSize * s, PlayerSize * s, focus, DotSize * s);

            // The game drew its corner trim under the map; draw the part over the map
            // again so the map sits inside the frame. Same placement as the game uses.
            Texture2D ui = InterfaceRender.interfaceTex;
            if (ui != null && ConfigMgr.hudVis < 1)
            {
                device.ScissorRectangle = edge;
                batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp,
                    DepthStencilState.None, MapView.Clipped);
                batch.Draw(ui, trim, TrimSource, new Color(1f, 1f, 1f, 0.45f), 0f,
                    new Vector2(448f - 64f, 64f), 0.5f * s, SpriteEffects.FlipHorizontally, 0f);
                batch.End();
            }

            if (!reported)
            {
                reported = true;
                Log.Info("minimap drew: HUD scale " + s.ToString("F2", CultureInfo.InvariantCulture)
                    + ", frame (" + frame.X + ", " + frame.Y + ") " + frame.Width + "x" + frame.Height
                    + ", player " + Format(PlayerTracker.Position) + " -> map pixel " + Format(focus)
                    + ", zoom " + zoom + " (" + scale.ToString("F3", CultureInfo.InvariantCulture) + " screen px per map px)");
            }
        }

        static string Format(Vector2 v)
        {
            return "(" + v.X.ToString("F1", CultureInfo.InvariantCulture) + ", " + v.Y.ToString("F1", CultureInfo.InvariantCulture) + ")";
        }
    }
}
