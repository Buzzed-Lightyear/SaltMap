using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectTower;
using ProjectTower.game;
using ProjectTower.gamestate;

namespace SaltMap
{
    /// <summary>
    /// Everything the mod draws on screen. Draw is called just before
    /// HUD.DrawModalDialog, so it lands above the game's picture and HUD and below
    /// modal dialogs, the mouse cursor and the save icon (see docs/rendering.md).
    /// The menu map draws earlier, from inside the game's menu, through
    /// DrawOutsideGameBatch.
    /// </summary>
    internal static class Overlay
    {
        // Our own batch, so the game's SpriteTools.sprite is never left mid-Begin by a bug here.
        static SpriteBatch batch;
        static Texture2D pixel;

        // Game states already reported, so each draw path (splash, title, game) is logged once.
        static readonly HashSet<int> reportedStates = new HashSet<int>();

        public static SpriteBatch Batch { get { return batch; } }
        public static Texture2D Pixel { get { return pixel; } }

        static void EnsureResources(GraphicsDevice device)
        {
            if (batch == null || batch.IsDisposed || batch.GraphicsDevice != device)
            {
                batch = new SpriteBatch(device);
                pixel = new Texture2D(device, 1, 1);
                pixel.SetData(new[] { Color.White });
            }
        }

        public static void Draw(GraphicsDevice device)
        {
            EnsureResources(device);

            int state = GameStateManager.gameState;
            if (reportedStates.Add(state))
            {
                Viewport view = device.Viewport;
                PresentationParameters pp = device.PresentationParameters;
                Log.Info("overlay hook in game state " + state + ": target " + BoundTarget(device)
                    + ", viewport " + view.Width + "x" + view.Height + " at (" + view.X + ", " + view.Y + ")"
                    + ", back buffer " + pp.BackBufferWidth + "x" + pp.BackBufferHeight);
            }

            try
            {
                Run(device, () => Minimap.Draw(device, batch, pixel));
                // Warm-up: the whole-world tile and the marker icons load on the title
                // screen already, so loading problems show in the log before a save.
                TileCache.Get(0, 0, 0);
                TileCache.Pump(device);
                Markers.PreloadIcons(device);
                if (state == 2) EntityDump.TryWrite(state);
            }
            finally
            {
                TileCache.EndFrame();
            }
        }

        /// <summary>
        /// Runs mod drawing from inside the game's own drawing: ends the game's batch if
        /// it is open, draws, puts the device state back, and reopens the batch the way
        /// the game's HUD code keeps it (SpriteTools.BeginAlpha), as InterfaceRender
        /// itself does around its glow passes.
        /// </summary>
        public static void DrawOutsideGameBatch(Action<GraphicsDevice> draw)
        {
            GraphicsDevice device = Game1.game.GraphicsDevice;
            EnsureResources(device);
            bool reopen = !GameApi.CanTellBegun || GameApi.IsBegun(SpriteTools.sprite);
            if (reopen) SpriteTools.End();
            try
            {
                Run(device, () => draw(device));
            }
            finally
            {
                if (reopen) SpriteTools.BeginAlpha();
            }
        }

        /// <summary>Runs drawing code and restores what SpriteBatch leaves on the device.</summary>
        static void Run(GraphicsDevice device, Action draw)
        {
            BlendState blend = device.BlendState;
            DepthStencilState depth = device.DepthStencilState;
            RasterizerState raster = device.RasterizerState;
            SamplerState sampler = device.SamplerStates[0];
            Rectangle scissor = device.ScissorRectangle;
            try
            {
                draw();
            }
            catch
            {
                // The batch may have been left inside Begin. Start fresh next time.
                batch.Dispose();
                pixel.Dispose();
                batch = null;
                throw;
            }
            finally
            {
                device.BlendState = blend;
                device.DepthStencilState = depth;
                device.RasterizerState = raster;
                device.SamplerStates[0] = sampler;
                device.ScissorRectangle = scissor;
            }
        }

        /// <summary>Names what the device is drawing into, using the game's own target names.</summary>
        static string BoundTarget(GraphicsDevice device)
        {
            RenderTargetBinding[] bound = device.GetRenderTargets();
            if (bound.Length == 0) return "back buffer (none bound)";

            Texture target = bound[0].RenderTarget;
            string name =
                target == GameDraw.mainTarg ? "GameDraw.mainTarg" :
                target == GameDraw.sceneTarg ? "GameDraw.sceneTarg" :
                target == GameDraw.backTarg ? "GameDraw.backTarg" :
                target == GameDraw.auxTarg ? "GameDraw.auxTarg" :
                target == GameDraw.lightTarg ? "GameDraw.lightTarg" :
                "an unnamed render target";
            RenderTarget2D target2D = target as RenderTarget2D;
            if (target2D != null) name += " " + target2D.Width + "x" + target2D.Height;
            return name + (bound.Length > 1 ? " (+" + (bound.Length - 1) + " more)" : "");
        }
    }
}
