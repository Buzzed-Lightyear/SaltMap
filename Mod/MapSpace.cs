using System;
using Microsoft.Xna.Framework;

namespace SaltMap
{
    /// <summary>
    /// "Map pixels": the SSMap web map's pixel space at its most detailed tile zoom (7),
    /// 32768 px square. Tiles, marker positions and the player are all placed in it.
    /// </summary>
    internal static class MapSpace
    {
        public const int MaxZoom = 7;
        public const int TileSize = 256;

        // World position to map pixel, fitted against the level's collision grid on a
        // stitched zoom-5 copy of the tiles (docs/minimap.md). Residual about 1 zoom-5
        // pixel, which is 4 map pixels or about 12 world units.
        const double OriginX = 133.5987, ScaleX = 0.31975016;
        const double OriginY = 8735.7470, ScaleY = 0.31987863;

        /// <summary>Map pixels per world unit, for sizing things.</summary>
        public const float PixelsPerUnit = 0.3198f;

        public static Vector2 FromWorld(Vector2 world)
        {
            return new Vector2((float)(OriginX + world.X * ScaleX), (float)(OriginY + world.Y * ScaleY));
        }

        public static Vector2 ToWorld(Vector2 map)
        {
            return new Vector2((float)((map.X - OriginX) / ScaleX), (float)((map.Y - OriginY) / ScaleY));
        }

        /// <summary>
        /// Leaflet's default Web Mercator projection at zoom 7, which is how SSMap stores
        /// marker positions (index.html uses the default CRS).
        /// </summary>
        public static Vector2 FromLatLng(double lat, double lng)
        {
            const double size = TileSize << MaxZoom;   // 32768
            const double maxLat = 85.0511287798;
            lat = Math.Max(-maxLat, Math.Min(maxLat, lat));
            double x = size * (lng / 360.0 + 0.5);
            double s = Math.Sin(lat * Math.PI / 180.0);
            double y = size * (0.5 - 0.25 * Math.Log((1 + s) / (1 - s)) / Math.PI);
            return new Vector2((float)x, (float)y);
        }
    }
}
