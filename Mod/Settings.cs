using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SaltMap
{
    /// <summary>
    /// Mods\SaltMap.ini: one "key=value" per line, "#" starts a comment. Written with
    /// defaults on first launch, and rewritten when the mod changes a value.
    /// </summary>
    internal static class Settings
    {
        // A folder named SSMap next to the mod, unless SaltMap.ini says otherwise.
        const string DefaultSsMap = "SSMap";

        static readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static string path;
        static string baseDir = "";

        public static void Load(string modDir)
        {
            baseDir = modDir;
            path = Path.Combine(modDir, "SaltMap.ini");
            if (!File.Exists(path))
            {
                values["ssmap"] = DefaultSsMap;
                values["minimap"] = "on";
                Save();
                Log.Info("settings: wrote defaults to " + path);
                return;
            }
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            Log.Info("settings: read " + path);
        }

        public static string Get(string key, string fallback)
        {
            string v;
            return values.TryGetValue(key, out v) && v.Length > 0 ? v : fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            int v;
            return int.TryParse(Get(key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public static bool GetBool(string key, bool fallback)
        {
            string v = Get(key, "");
            if (v.Equals("on", StringComparison.OrdinalIgnoreCase) || v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (v.Equals("off", StringComparison.OrdinalIgnoreCase) || v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        /// <summary>The SSMap folder; a relative path counts from the Mods folder.</summary>
        public static string SsMapFolder
        {
            get
            {
                string v = Get("ssmap", DefaultSsMap);
                return Path.IsPathRooted(v) ? v : Path.Combine(baseDir, v);
            }
        }

        /// <summary>
        /// Changes one setting and writes just that line, re-reading the file first, so
        /// edits made while the game runs (by hand, or by the installer) are kept.
        /// </summary>
        public static void Set(string key, string value)
        {
            values[key] = value;
            try
            {
                if (path == null) return;
                if (!File.Exists(path)) { Save(); return; }
                List<string> lines = new List<string>(File.ReadAllLines(path));
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || eq <= 0) continue;
                    if (!line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                    lines[i] = key + "=" + value;
                    found = true;
                }
                if (!found) lines.Add(key + "=" + value);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception e) { Log.ErrorOnce("Settings.Set", e); }
        }

        static void Save()
        {
            if (path == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# SaltMap settings. Lines are key=value; # starts a comment.");
            sb.AppendLine("# ssmap: folder of the SSMap web map (needs tiles\\, markers.js and images\\markerIcons\\);");
            sb.AppendLine("#        a relative path counts from this Mods folder.");
            sb.AppendLine("# minimap: on or off. minimapZoom: 0 (far) to 7 (near).");
            foreach (KeyValuePair<string, string> kv in values)
                sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(path, sb.ToString());
        }
    }
}
