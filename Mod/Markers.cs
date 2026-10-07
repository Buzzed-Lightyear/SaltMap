using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SaltMap
{
    internal sealed class Marker
    {
        public string Category;     // Items, NPC, Enemies, Sanctuaries, Spells & Prayers
        public string Group;        // Weapons, Rings, Bosses, Candles, ...
        public string Name;         // SSMap's tooltip, cleaned for the game's font
        public string Icon;         // file in images\markerIcons
        public Vector2 Map;         // map pixels (MapSpace)
    }

    /// <summary>
    /// Item, NPC, boss and sanctuary markers from SSMap's markers.js, plus its region
    /// labels. Positions are Leaflet latitude/longitude, converted to map pixels, so
    /// they line up with the tiles by construction.
    /// </summary>
    internal static class Markers
    {
        [DataContract]
        sealed class JsonMarker
        {
            [DataMember] public string icoPath = null;
            [DataMember] public string group = null;
            [DataMember] public string category = null;
            [DataMember] public string tooltip = null;
            [DataMember] public string location = null;
            [DataMember] public double lat = 0;
            [DataMember] public double lng = 0;
        }

        public static readonly List<Marker> All = new List<Marker>();
        public static readonly List<Marker> Regions = new List<Marker>();

        static string iconFolder;
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public static void Load(string ssmapFolder)
        {
            iconFolder = Path.Combine(ssmapFolder, Path.Combine("images", "markerIcons"));
            string file = Path.Combine(ssmapFolder, "markers.js");
            if (!File.Exists(file)) { Log.Info("markers: " + file + " not found"); return; }

            string js = File.ReadAllText(file, Encoding.UTF8);
            foreach (JsonMarker m in Parse(js, "getAllMarkersData"))
            {
                All.Add(new Marker
                {
                    Category = m.category ?? "",
                    Group = m.group ?? "",
                    Name = Clean(m.tooltip ?? m.group ?? ""),
                    Icon = m.icoPath,
                    Map = MapSpace.FromLatLng(m.lat, m.lng),
                });
            }
            foreach (JsonMarker m in Parse(js, "getLocationMarkersData"))
                Regions.Add(new Marker { Category = "Region", Name = Clean(m.location ?? ""), Map = MapSpace.FromLatLng(m.lat, m.lng) });

            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (Marker m in All)
            {
                int n;
                counts.TryGetValue(m.Category, out n);
                counts[m.Category] = n + 1;
            }
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> kv in counts) sb.Append(", ").Append(kv.Key).Append(' ').Append(kv.Value);
            Log.Info("markers: " + All.Count + " from " + file + sb + "; regions " + Regions.Count);
        }

        /// <summary>
        /// markers.js holds each list as JSON inside a JavaScript template string
        /// (function name() { return `[...]`; }), with every backslash doubled.
        /// </summary>
        static List<JsonMarker> Parse(string js, string function)
        {
            int at = js.IndexOf(function, StringComparison.Ordinal);
            int open = at < 0 ? -1 : js.IndexOf('`', at);
            int close = open < 0 ? -1 : js.IndexOf('`', open + 1);
            if (close < 0) { Log.Info("markers: " + function + " not found in markers.js"); return new List<JsonMarker>(); }
            string json = js.Substring(open + 1, close - open - 1).Replace("\\\\", "\\");
            DataContractJsonSerializer reader = new DataContractJsonSerializer(typeof(List<JsonMarker>));
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (List<JsonMarker>)reader.ReadObject(ms);
        }

        /// <summary>Keeps characters the game's font is sure to have. The file's broken quotes become apostrophes.</summary>
        static string Clean(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= 32 && c < 127) sb.Append(c);
                else if (c == '�' || c == '’' || c == '‘') sb.Append('\'');
                else if (c == '“' || c == '”') sb.Append('"');
                else if (c == '\n' || c == '\t') sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        static bool preloaded;

        /// <summary>Loads every marker's icon once and logs how many there are. Game thread only.</summary>
        public static void PreloadIcons(GraphicsDevice device)
        {
            if (preloaded) return;
            preloaded = true;
            int ok = 0, names = 0;
            foreach (Marker m in All)
            {
                if (icons.ContainsKey(m.Icon ?? "")) continue;
                names++;
                if (Icon(device, m.Icon) != null) ok++;
            }
            Log.Info("markers: " + ok + " of " + names + " icons loaded from " + iconFolder);
        }

        /// <summary>The marker's icon, loaded on first use (they are small). Game thread only.</summary>
        public static Texture2D Icon(GraphicsDevice device, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Texture2D tex;
            if (icons.TryGetValue(name, out tex)) return tex;
            tex = null;
            try
            {
                string path = Path.Combine(iconFolder, name);
                if (File.Exists(path)) tex = Images.Decode(path).Upload(device);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("marker icon", e);
            }
            icons[name] = tex;
            return tex;
        }
    }
}
