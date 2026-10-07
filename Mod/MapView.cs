using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SaltMap
{
    /// <summary>
    /// Draws part of the map into a screen rectangle: tiles, marker icons and the
    /// player. <c>focus</c> (map pixels) lands on the rectangle's centre and
    /// <c>scale</c> is screen pixels per map pixel. Uses its own SpriteBatch and a
    /// scissor rectangle; the caller restores device state afterwards.
    /// </summary>
    internal static class MapView
    {
        /// <summary>No culling, clipped to the device's scissor rectangle.</summary>
        public static readonly RasterizerState Clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
        static readonly Color Background = new Color(8, 8, 12);
        static readonly Color PlayerColor = new Color(230, 40, 40);

        public static Vector2 ToScreen(Rectangle rect, Vector2 focus, float scale, Vector2 map)
        {
            return new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) + (map - focus) * scale;
        }

        public static Vector2 ToMap(Rectangle rect, Vector2 focus, float scale, Vector2 screen)
        {
            return focus + (screen - new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f)) / scale;
        }

        static readonly Color LiveEnemy = new Color(235, 30, 30);
        static readonly Color FarEnemy = new Color(200, 40, 40, 150);
        static Texture2D dot;

        /// <summary>A soft white disc for the enemy dots, made once.</summary>
        static Texture2D Dot(GraphicsDevice device)
        {
            if (dot != null && !dot.IsDisposed) return dot;
            const int n = 32;
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    float a = MathHelper.Clamp(n / 2f - 1f - d, 0f, 1f);    // one pixel of soft edge
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            dot = new Texture2D(device, n, n);
            dot.SetData(px);
            return dot;
        }

        public static void Draw(GraphicsDevice device, SpriteBatch batch, Texture2D pixel, Rectangle rect,
            Vector2 focus, float scale, float iconSize, float playerSize, Vector2? player, float dotSize)
        {
            TileCache.Pump(device);
            device.ScissorRectangle = rect;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp,
                DepthStencilState.None, Clipped);
            batch.Draw(pixel, rect, Background);

            // The tile zoom that is at least as sharp as the screen needs, a little leniently.
            int z = (int)Math.Ceiling(MapSpace.MaxZoom + Math.Log(scale, 2) - 0.3);
            z = Math.Max(0, Math.Min(MapSpace.MaxZoom, z));
            // A coarse layer first, so there is something to see while sharper tiles load.
            int coarse = Math.Max(0, z - 2);
            if (coarse < z) DrawTiles(batch, rect, focus, scale, coarse, false);
            DrawTiles(batch, rect, focus, scale, z, true);

            if (dotSize > 0)
            {
                Texture2D disc = Dot(device);
                // Unspawned ones first, so live ones sit on top.
                for (int pass = 0; pass < 2; pass++)
                    foreach (Enemies.Dot e in Enemies.Dots)
                    {
                        if (e.Live != (pass == 1)) continue;
                        float size = dotSize * (e.Boss ? 2f : 1f) * (e.Live ? 1f : 0.75f);
                        Vector2 at = ToScreen(rect, focus, scale, e.Map);
                        if (at.X < rect.Left - size || at.X > rect.Right + size || at.Y < rect.Top - size || at.Y > rect.Bottom + size) continue;
                        // The position is the feet; lift the dot to about the body.
                        at.Y -= size * 0.5f;
                        batch.Draw(disc, new Rectangle((int)Math.Round(at.X - size / 2), (int)Math.Round(at.Y - size / 2),
                            (int)Math.Round(size), (int)Math.Round(size)), e.Live ? LiveEnemy : FarEnemy);
                    }
            }

            if (iconSize > 0)
            {
                float half = iconSize / 2f;
                foreach (Marker m in Markers.All)
                {
                    if (Progress.IsDone(m)) continue;
                    Vector2 at = ToScreen(rect, focus, scale, m.Map);
                    if (at.X < rect.Left - half || at.X > rect.Right + half || at.Y < rect.Top - half || at.Y > rect.Bottom + half) continue;
                    Texture2D icon = Markers.Icon(device, m.Icon);
                    if (icon == null) continue;
                    batch.Draw(icon, new Rectangle((int)Math.Round(at.X - half), (int)Math.Round(at.Y - half),
                        (int)Math.Round(iconSize), (int)Math.Round(iconSize)), Color.White);
                }
            }

            if (player.HasValue)
            {
                // A diamond whose bottom tip is at the feet (the player's position is the feet).
                Vector2 feet = ToScreen(rect, focus, scale, player.Value);
                Vector2 middle = new Vector2(feet.X, feet.Y - playerSize);
                DrawDiamond(batch, pixel, middle, playerSize + Math.Max(2f, playerSize * 0.3f), Color.Black);
                DrawDiamond(batch, pixel, middle, playerSize, PlayerColor);
            }
            batch.End();
        }

        static void DrawTiles(SpriteBatch batch, Rectangle rect, Vector2 focus, float scale, int z, bool request)
        {
            // Map pixels covered by one tile at this zoom.
            float span = MapSpace.TileSize * (float)Math.Pow(2, MapSpace.MaxZoom - z);
            Vector2 topLeft = ToMap(rect, focus, scale, new Vector2(rect.Left, rect.Top));
            Vector2 bottomRight = ToMap(rect, focus, scale, new Vector2(rect.Right, rect.Bottom));
            int last = (1 << z) - 1;
            int x0 = Math.Max(0, (int)Math.Floor(topLeft.X / span)), x1 = Math.Min(last, (int)Math.Floor(bottomRight.X / span));
            int y0 = Math.Max(0, (int)Math.Floor(topLeft.Y / span)), y1 = Math.Min(last, (int)Math.Floor(bottomRight.Y / span));
            for (int ty = y0; ty <= y1; ty++)
            {
                for (int tx = x0; tx <= x1; tx++)
                {
                    Texture2D tex = request ? TileCache.Get(z, tx, ty) : TileCache.Peek(z, tx, ty);
                    if (tex == null)
                    {
                        if (!request) TileCache.Get(z, tx, ty);   // keep the coarse layer coming too
                        continue;
                    }
                    // Corners rounded to whole pixels from shared edges, so tiles meet without gaps.
                    Vector2 a = ToScreen(rect, focus, scale, new Vector2(tx * span, ty * span));
                    Vector2 b = ToScreen(rect, focus, scale, new Vector2((tx + 1) * span, (ty + 1) * span));
                    int left = (int)Math.Round(a.X), top = (int)Math.Round(a.Y);
                    batch.Draw(tex, new Rectangle(left, top, (int)Math.Round(b.X) - left, (int)Math.Round(b.Y) - top), Color.White);
                }
            }
        }

        public static void DrawDiamond(SpriteBatch batch, Texture2D pixel, Vector2 at, float radius, Color color)
        {
            float side = radius * 1.41421356f;
            batch.Draw(pixel, at, null, color, MathHelper.PiOver4, new Vector2(0.5f, 0.5f),
                new Vector2(side, side), SpriteEffects.None, 0f);
        }

        /// <summary>The marker drawn closest to a screen point, within <paramref name="reach"/> pixels, or null.</summary>
        public static Marker Nearest(Rectangle rect, Vector2 focus, float scale, Vector2 screen, float reach)
        {
            Marker best = null;
            float bestDist = reach * reach;
            foreach (Marker m in Markers.All)
            {
                if (Progress.IsDone(m)) continue;
                float d = Vector2.DistanceSquared(ToScreen(rect, focus, scale, m.Map), screen);
                if (d < bestDist) { bestDist = d; best = m; }
            }
            return best;
        }
    }
}
