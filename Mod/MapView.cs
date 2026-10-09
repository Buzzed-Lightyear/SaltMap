using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SaltMap
{
    /// <summary>What a map view draws and how big, in screen pixels.</summary>
    internal struct ViewStyle
    {
        public float IconSize;
        public float PlayerSize;
        public float DotSize;
        public Show Show;
        public bool Guide;          // ring the next objective
        public bool EdgePointer;    // and point at it from the edge when it is outside
        public bool OnlyRinged;     // zoomed far out: only the ringed objectives' icons, no enemies
        public float EnemyRange;    // world units around the player; 0 shows every enemy
    }

    /// <summary>
    /// Draws part of the map into a screen rectangle: tiles, enemy dots, marker icons,
    /// the next objective, and the players. <c>focus</c> (map pixels) lands on the
    /// rectangle's centre and <c>scale</c> is screen pixels per map pixel. Uses its own
    /// SpriteBatch and a scissor rectangle; the caller restores device state afterwards.
    /// Players and enemies are plain shapes: a diamond for each player, outlined dots for
    /// enemies near the player, bosses ringed.
    /// </summary>
    internal static class MapView
    {
        /// <summary>No culling, clipped to the device's scissor rectangle.</summary>
        public static readonly RasterizerState Clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
        static readonly Color Background = new Color(8, 8, 12);
        static readonly Color PlayerColor = new Color(255, 70, 215);   // magenta: no other icon uses it
        static readonly Color CoopColor = new Color(70, 150, 255);
        static readonly Color Outline = new Color(0, 0, 0, 230);
        static readonly Color EnemyColor = new Color(235, 35, 30);
        static readonly Color BossRing = new Color(235, 195, 80);
        static readonly Color GuideColor = new Color(255, 210, 70);
        static readonly Color OptionalColor = new Color(150, 220, 255);

        static Texture2D dot, ring;

        public static Vector2 ToScreen(Rectangle rect, Vector2 focus, float scale, Vector2 map)
        {
            return new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) + (map - focus) * scale;
        }

        public static Vector2 ToMap(Rectangle rect, Vector2 focus, float scale, Vector2 screen)
        {
            return focus + (screen - new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f)) / scale;
        }

        /// <summary>A soft white disc (inner radius 0) or ring, made once.</summary>
        static Texture2D Disc(GraphicsDevice device, ref Texture2D cache, float inner)
        {
            if (cache != null && !cache.IsDisposed) return cache;
            const int n = 64;
            Color[] px = new Color[n * n];
            float outer = n / 2f - 1f, hole = outer * inner;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    float a = MathHelper.Clamp(outer - d, 0f, 1f);                  // soft outer edge
                    if (inner > 0f) a *= MathHelper.Clamp(d - hole, 0f, 1f);        // soft inner edge
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            cache = new Texture2D(device, n, n);
            cache.SetData(px);
            return cache;
        }

        public static void Draw(GraphicsDevice device, SpriteBatch batch, Texture2D pixel, Rectangle rect,
            Vector2 focus, float scale, ViewStyle style)
        {
            TileCache.Pump(device);
            Texture2D disc = Disc(device, ref dot, 0f);
            Texture2D hoop = Disc(device, ref ring, 0.58f);
            float t = PlayerTracker.Now;

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

            if (style.DotSize > 0 && (style.Show & Show.Enemies) != 0 && !style.OnlyRinged)
                DrawEnemies(batch, disc, hoop, rect, focus, scale, style);

            if (style.IconSize > 0)
            {
                float half = style.IconSize / 2f;
                HashSet<Marker> ringed = style.OnlyRinged ? Guide.RingedMarkers() : null;
                foreach (Marker m in Markers.All)
                {
                    if (!Shown(m, style, ringed)) continue;
                    Vector2 at = ToScreen(rect, focus, scale, m.Map);
                    if (!Near(rect, at, half)) continue;
                    Texture2D icon = Markers.Icon(device, Progress.IconOf(m));
                    if (icon == null) continue;
                    batch.Draw(icon, new Rectangle((int)Math.Round(at.X - half), (int)Math.Round(at.Y - half),
                        (int)Math.Round(style.IconSize), (int)Math.Round(style.IconSize)), Color.White);
                }
            }

            string nextText;
            Vector2 next;
            if (style.Guide)
            {
                // Optional objectives: pale blue rings, a little smaller and slower than the
                // main one, only where they are in view.
                float r = Math.Max(style.IconSize, 12f) * 1.75f * (1f + 0.1f * (float)Math.Sin(t * 3f));
                foreach (KeyValuePair<string, Vector2> o in Guide.OptionalOpen())
                {
                    Vector2 at = ToScreen(rect, focus, scale, o.Value);
                    if (!Near(rect, at, r)) continue;
                    Blit(batch, hoop, at, r + 5f, Outline * 0.75f);
                    Blit(batch, hoop, at, r, OptionalColor);
                }
                if (Guide.Next(out nextText, out next))
                    DrawObjective(batch, pixel, hoop, rect, ToScreen(rect, focus, scale, next), style, t);
            }

            if (style.PlayerSize > 0 && PlayerTracker.HasPosition)
            {
                if (PlayerTracker.CoopPosition.HasValue)
                    DrawPlayer(batch, pixel, ToScreen(rect, focus, scale, MapSpace.FromWorld(PlayerTracker.CoopPosition.Value)), style.PlayerSize, CoopColor);
                DrawPlayer(batch, pixel, ToScreen(rect, focus, scale, MapSpace.FromWorld(PlayerTracker.Position)), style.PlayerSize, PlayerColor);
            }
            batch.End();
        }

        static bool Near(Rectangle rect, Vector2 at, float margin)
        {
            return at.X >= rect.Left - margin && at.X <= rect.Right + margin && at.Y >= rect.Top - margin && at.Y <= rect.Bottom + margin;
        }

        /// <summary>Whether a marker is drawn: in the filter, not done, and ringed when only those show.</summary>
        static bool Shown(Marker m, ViewStyle style, HashSet<Marker> ringed)
        {
            if ((style.Show & m.Kind) == 0 || Progress.IsHidden(m)) return false;
            return ringed == null || ringed.Contains(m);
        }

        /// <summary>Whether an enemy is close enough to the player to show.</summary>
        static bool InRange(Enemies.Dot e, ViewStyle style)
        {
            if (style.EnemyRange <= 0f || !PlayerTracker.HasPosition) return true;
            float reach = style.EnemyRange * MapSpace.PixelsPerUnit;
            return Vector2.DistanceSquared(e.Map, MapSpace.FromWorld(PlayerTracker.Position)) <= reach * reach;
        }

        /// <summary>Enemies near the player: red dots with a dark edge, bosses larger and ringed.</summary>
        static void DrawEnemies(SpriteBatch batch, Texture2D disc, Texture2D hoop, Rectangle rect, Vector2 focus, float scale, ViewStyle style)
        {
            foreach (Enemies.Dot e in Enemies.Dots)
            {
                if (!InRange(e, style)) continue;
                float size = style.DotSize * (e.Boss ? 1.5f : 1f);
                Vector2 at = ToScreen(rect, focus, scale, e.Map);
                if (!Near(rect, at, size * 1.5f)) continue;
                at.Y -= size * 0.6f;                                     // from the feet up to the body
                if (e.Boss) Blit(batch, hoop, at, size * 1.7f, BossRing * 0.85f);
                Blit(batch, disc, at, size + 2f, Outline * 0.6f);
                Blit(batch, disc, at, size, EnemyColor);
            }
        }

        /// <summary>A diamond whose bottom tip is at the feet (the position is the feet).</summary>
        static void DrawPlayer(SpriteBatch batch, Texture2D pixel, Vector2 feet, float size, Color color)
        {
            Vector2 middle = new Vector2(feet.X, feet.Y - size);
            DrawDiamond(batch, pixel, middle, size + Math.Max(2f, size * 0.3f), Color.Black);
            DrawDiamond(batch, pixel, middle, size, color);
        }

        /// <summary>The next objective: a pulsing ring on it, or an arrow at the edge pointing to it.</summary>
        static void DrawObjective(SpriteBatch batch, Texture2D pixel, Texture2D hoop, Rectangle rect, Vector2 at, ViewStyle style, float t)
        {
            float pulse = 1f + 0.15f * (float)Math.Sin(t * 4f);
            float radius = Math.Max(style.IconSize, 12f) * 2.1f * pulse;
            if (Near(rect, at, -radius * 0.3f))
            {
                Blit(batch, hoop, at, radius + 6f, Outline * 0.8f);
                Blit(batch, hoop, at, radius, GuideColor);
                return;
            }
            if (!style.EdgePointer) return;
            Vector2 centre = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            Vector2 d = at - centre;
            if (d.LengthSquared() < 1f) return;
            float size = Math.Max(style.IconSize, 12f) * 0.9f;
            float inset = size * 1.4f;
            float sx = d.X != 0f ? (rect.Width / 2f - inset) / Math.Abs(d.X) : float.MaxValue;
            float sy = d.Y != 0f ? (rect.Height / 2f - inset) / Math.Abs(d.Y) : float.MaxValue;
            Vector2 tip = centre + d * Math.Min(sx, sy);
            float angle = (float)Math.Atan2(d.Y, d.X);
            float thick = Math.Max(2.5f, size * 0.42f) * pulse;
            DrawChevron(batch, pixel, tip, angle, size, thick + 3f, Outline);
            DrawChevron(batch, pixel, tip, angle, size, thick, GuideColor);
        }

        static void Blit(SpriteBatch batch, Texture2D tex, Vector2 centre, float size, Color color)
        {
            batch.Draw(tex, centre, null, color, 0f, new Vector2(tex.Width / 2f, tex.Height / 2f),
                size / tex.Width, SpriteEffects.None, 0f);
        }

        /// <summary>A "&gt;" pointing along <paramref name="angle"/> with its point at <paramref name="tip"/>.</summary>
        public static void DrawChevron(SpriteBatch batch, Texture2D pixel, Vector2 tip, float angle, float length, float thick, Color color)
        {
            foreach (float arm in new[] { angle + MathHelper.Pi * 0.78f, angle - MathHelper.Pi * 0.78f })
                batch.Draw(pixel, tip, null, color, arm, new Vector2(0f, 0.5f), new Vector2(length, thick), SpriteEffects.None, 0f);
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
        public static Marker Nearest(Rectangle rect, Vector2 focus, float scale, Vector2 screen, float reach, ViewStyle style)
        {
            Marker best = null;
            float bestDist = reach * reach;
            HashSet<Marker> ringed = style.OnlyRinged ? Guide.RingedMarkers() : null;
            foreach (Marker m in Markers.All)
            {
                if (!Shown(m, style, ringed)) continue;
                float d = Vector2.DistanceSquared(ToScreen(rect, focus, scale, m.Map), screen);
                if (d < bestDist) { bestDist = d; best = m; }
            }
            return best;
        }

        /// <summary>The guide objective closest to a screen point ("Next: ..." or "Optional: ..."), or null.</summary>
        public static string NearestObjective(Rectangle rect, Vector2 focus, float scale, Vector2 screen, float reach)
        {
            string best = null;
            float bestDist = reach * reach;
            string text;
            Vector2 at;
            if (Guide.Next(out text, out at))
            {
                float d = Vector2.DistanceSquared(ToScreen(rect, focus, scale, at), screen);
                if (d < bestDist) { bestDist = d; best = "Next: " + text; }
            }
            foreach (KeyValuePair<string, Vector2> o in Guide.OptionalOpen())
            {
                float d = Vector2.DistanceSquared(ToScreen(rect, focus, scale, o.Value), screen);
                if (d < bestDist) { bestDist = d; best = "Optional: " + o.Key; }
            }
            return best;
        }

        /// <summary>The enemy drawn closest to a screen point, within <paramref name="reach"/> pixels.</summary>
        public static bool NearestEnemy(Rectangle rect, Vector2 focus, float scale, Vector2 screen, float reach, ViewStyle style, out Enemies.Dot found)
        {
            found = default(Enemies.Dot);
            bool any = false;
            float bestDist = reach * reach;
            if (style.OnlyRinged || (style.Show & Show.Enemies) == 0) return false;
            foreach (Enemies.Dot e in Enemies.Dots)
            {
                if (!InRange(e, style)) continue;
                float d = Vector2.DistanceSquared(ToScreen(rect, focus, scale, e.Map), screen);
                if (d < bestDist) { bestDist = d; found = e; any = true; }
            }
            return any;
        }
    }
}
