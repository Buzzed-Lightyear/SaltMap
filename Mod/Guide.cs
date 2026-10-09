using System;
using System.Collections.Generic;
using MapEdit.map;
using Microsoft.Xna.Framework;

namespace SaltMap
{
    /// <summary>
    /// "Where next": the route through the game after the Fextralife wiki's Game Progress
    /// Route (https://saltandsanctuary.wiki.fextralife.com/Game_Progress_Route).
    ///
    /// The main route's next objective is its first step not done in this save. Optional
    /// steps (side bosses, NPC stories, notable items, hidden sanctuaries) show once the
    /// main step that opens them is done, so they do not spoil later areas. Each step
    /// has a test the save can answer:
    ///   boss       the boss's flag ("boss X" in its script) is set
    ///   brand      the player owns the brand (Player.runes)
    ///   flag       a flag is set
    ///   story      an NPC's story ends (its final reward's flag); points at the NPC's
    ///              current spot, by the same rules as the NPC icons
    ///   item       an SSMap item icon that is tracked is done; points at the icon
    ///   sanctuary  the sanctuary has been visited (Player.sanctuaryVisitCount)
    /// Wiki steps without such a test (passing through an area, meeting a merchant, the
    /// endings) are left out; the next objective leads through those places anyway. So is
    /// the prologue: the Unspeakable Deep on the boat is a scripted fight that never sets
    /// its flag, and the boat is not on SSMap's map.
    ///
    /// Modes (guide= in SaltMap.ini; Y or G on the map page cycles): all (main and
    /// optional), main (main route only), off.
    /// </summary>
    internal static class Guide
    {
        enum Kind { Boss, Brand, Flag, Story, Item, Sanctuary }

        sealed class Step
        {
            public Kind Kind;
            public string Text;          // "Defeat The Sodden Knight"
            public string Area;          // "The Festering Banquet"
            public string Key;           // boss flag, flag, or the story's final flag
            public int Rune = -1;        // Player.runes index for a brand
            public string Npc;           // NPC texture (brand giver, story, old man)
            public string Marker;        // SSMap marker name (items, sanctuaries)
            public string After;         // the main step that opens an optional one: its boss flag or NPC
            public Vector2? Target;      // world position, for fixed targets
        }

        static Step Boss(string boss, string area, string flag) { return new Step { Kind = Kind.Boss, Text = "Defeat " + boss, Area = area, Key = flag }; }
        static Step Brand(string what, string area, int rune, string npc) { return new Step { Kind = Kind.Brand, Text = what, Area = area, Rune = rune, Npc = npc }; }

        static readonly Step[] Main =
        {
            new Step { Kind = Kind.Flag, Text = "Choose a creed with the old man", Area = "Shivering Shore", Key = "chose_creed", Npc = "oldman" },
            Boss("The Sodden Knight", "The Festering Banquet", "dread"),                                     // 2
            Boss("The Queen of Smiles", "Village of Smiles", "cutqueen"),                                    // 3
            Boss("The Mad Alchemist", "The Watching Woods", "alchemist"),
            Boss("Kraekan Cyclops", "The Watching Woods", "bull"),
            Boss("The False Jester", "Sunken Keep", "fauxjester"),
            Brand("Get the Vertigo Brand from the Jester", "Sunken Keep", 0, "jester"),
            Boss("Kraekan Wyrm", "Castle of Storms", "dragon"),                                              // 8
            Brand("Get the Shadowflip Brand from the Despondent Thief", "Castle of Storms", 2, "despondent"),  // 9
            Boss("The Tree of Men", "Red Hall of Cages", "torturetree"),
            Boss("The Disemboweled Husk", "Hager's Cavern", "pirate"),
            Boss("That Stench Most Foul", "Mire of Stench", "gasbag"),                                       // 12
            Brand("Get the Redshift Brand from the Mirekeeper", "Mire of Stench", 4, "swampfriend"),
            Boss("The Untouched Inquisitor", "Dome of the Forgotten", "inquisitor"),
            Boss("The Third Lamb", "Dome of the Forgotten", "griffin"),
            Brand("Get the Hardlight Brand from the Luna Sage", "Dome of the Forgotten", 5, "domefriend"),   // 16
            Boss("The Dried King", "Ziggurat of Dust", "mummy"),                                              // 17
            Brand("Get the Dart Brand from the Black Sands Sorcerer", "Ziggurat of Dust", 1, "choppy"),      // 18
            Boss("Murdiella Mal", "Mal's Floating Castle", "murderfly"),
            Boss("The Bloodless Prince", "Ziggurat of Dust", "clay"),
            Boss("The Coveted", "The Ruined Temple", "ruinaxe"),                                              // 21
            Boss("The Witch of the Lake", "Siam Lake", "lakewitch"),                                          // 22
            Boss("The Architect and The Unskinned", "Salt Alkymancery", "monster"),
            Boss("Kraekan Dragon Skourzh", "Crypt of Dead Gods", "squiddragon"),                             // 24
            Boss("The Nameless God", "The Still Palace", "nameless"),
        };

        static readonly Step[] Optional =
        {
            new Step { Kind = Kind.Story, Text = "Masterless Knight's story", Area = null, Npc = "masterless", Key = "masterless_6_talk_2", After = "dread" },
            new Step { Kind = Kind.Item, Text = "Pitchfork", Area = "Bandit's Pass", Marker = "Pitchfork", After = "dread" },
            new Step { Kind = Kind.Item, Text = "Stone Sellsword", Area = "Bandit's Pass", Marker = "Stone Sellsword", After = "dread" },
            new Step { Kind = Kind.Item, Text = "Kureimoa", Area = "Village of Smiles", Marker = "Kureimoa", After = "dread" },
            new Step { Kind = Kind.Item, Text = "Haymaker", Area = "Village of Smiles", Marker = "Haymaker", After = "dread" },
            new Step { Kind = Kind.Story, Text = "Despondent Thief's story", Area = null, Npc = "despondent", Key = "4_desp_talk_3", After = "despondent" },
            new Step { Kind = Kind.Item, Text = "Vile Vines Ring", Area = "Fort-Beyond-The-Mire", Marker = "Vile Vines Ring", After = "gasbag" },
            new Step { Kind = Kind.Boss, Text = "Defeat Ronin Cran", Area = "Cran's Pass", Key = "broken", After = "domefriend" },
            new Step { Kind = Kind.Sanctuary, Text = "House of Splendor sanctuary", Area = "Cran's Pass", Marker = "The House of Splendor", After = "domefriend" },
            new Step { Kind = Kind.Item, Text = "Bag of Earth", Area = "Ziggurat of Dust", Marker = "Bag of Earth", After = "mummy" },
            new Step { Kind = Kind.Story, Text = "Black Sands Sorcerer's story", Area = null, Npc = "choppy", Key = "3_choppy_talk_4", After = "choppy" },
            new Step { Kind = Kind.Boss, Text = "Defeat Carsejaw the Cruel", Area = "Pitchwoods", Key = "cloak", After = "ruinaxe" },
            new Step { Kind = Kind.Sanctuary, Text = "Order of the Betrayer sanctuary", Area = "The Blackest Vault", Marker = "Order of the Betrayer", After = "lakewitch" },
            new Step { Kind = Kind.Boss, Text = "Defeat The Forgotten King", Area = "Crypt of Dead Gods", Key = "deadking", After = "squiddragon" },
        };

        public enum Mode { All, MainOnly, Off }
        static Mode mode;
        static bool loaded;

        public static Mode Current
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    string v = Settings.Get("guide", "all").ToLowerInvariant();
                    mode = v == "off" ? Mode.Off : v == "main" ? Mode.MainOnly : Mode.All;
                }
                return mode;
            }
        }

        public static bool Enabled { get { return Current != Mode.Off; } }

        /// <summary>All, main only, off, all...</summary>
        public static void Cycle()
        {
            mode = Current == Mode.All ? Mode.MainOnly : Current == Mode.MainOnly ? Mode.Off : Mode.All;
            Settings.Set("guide", mode == Mode.Off ? "off" : mode == Mode.MainOnly ? "main" : "all");
            Log.Info("guide " + Describe());
        }

        public static string Describe()
        {
            return Current == Mode.Off ? "off" : Current == Mode.MainOnly ? "main route" : "main route and optional";
        }

        /// <summary>Finds fixed targets in the level. Called when the level (re)loads.</summary>
        public static void Link(Seg[] segs)
        {
            int found = 0, total = 0;
            foreach (Step s in AllSteps())
            {
                s.Target = null;
                if (s.Kind == Kind.Boss || s.Kind == Kind.Brand || s.Kind == Kind.Flag)
                {
                    total++;
                    for (int k = 0; k < segs.Length && s.Target == null; k++)
                    {
                        Seg seg = segs[k];
                        if (seg == null) continue;
                        string script = seg.strFlag ?? "";
                        if (s.Kind == Kind.Boss && HasLine(script, "boss", s.Key)) s.Target = seg.loc;
                        else if (s.Kind == Kind.Brand && seg.texture == s.Npc && HasLine(script, "brandfire", null)) s.Target = seg.loc;
                        else if (s.Kind == Kind.Flag && seg.texture == s.Npc) s.Target = seg.loc;   // the NPC's first spot
                    }
                    if (s.Target.HasValue) found++;
                }
            }
            Log.Info("guide: " + found + " of " + total + " boss, brand and flag targets found in the level");
        }

        static IEnumerable<Step> AllSteps()
        {
            foreach (Step s in Main) yield return s;
            foreach (Step s in Optional) yield return s;
        }

        static bool HasLine(string script, string keyword, string value)
        {
            foreach (string raw in script.Split('\n'))
            {
                string[] t = raw.TrimEnd('\r').Split(' ');
                if (t[0] == keyword && (value == null || (t.Length > 1 && t[1] == value))) return true;
            }
            return false;
        }

        static bool Done(Step s)
        {
            switch (s.Kind)
            {
                case Kind.Brand: return Progress.HasRune(s.Rune);
                case Kind.Item:
                    {
                        Marker m = Markers.Find(s.Marker);
                        return m != null && Progress.IsDone(m);
                    }
                case Kind.Sanctuary:
                    {
                        Marker m = Markers.Find(s.Marker);
                        return m != null && Places.Visited(m);
                    }
                default: return Progress.Has(s.Key);
            }
        }

        /// <summary>Where the step points now, in map pixels, or null when it cannot be placed.</summary>
        static Vector2? Where(Step s)
        {
            switch (s.Kind)
            {
                case Kind.Story:
                    {
                        Vector2 w;
                        return Places.CurrentSpot(s.Npc, out w) ? MapSpace.FromWorld(w) : (Vector2?)null;
                    }
                case Kind.Item:
                case Kind.Sanctuary:
                    {
                        Marker m = Markers.Find(s.Marker);
                        // An item counts only if its icon is tracked, so "done" can be trusted.
                        if (m == null || (s.Kind == Kind.Item && !Progress.IsTracked(m))) return null;
                        return m.Map;
                    }
                default:
                    return s.Target.HasValue ? MapSpace.FromWorld(s.Target.Value) : (Vector2?)null;
            }
        }

        static string Describe(Step s)
        {
            string area = s.Area;
            if (area == null)
            {
                // A story's NPC moves: name the area where they stand now.
                Vector2? at = Where(s);
                area = at.HasValue ? Markers.RegionNear(at.Value) : null;
            }
            return area == null ? s.Text : s.Text + " (" + area + ")";
        }

        /// <summary>The main route's next objective, or false when it is done or the guide is off.</summary>
        public static bool Next(out string text, out Vector2 map)
        {
            text = null;
            map = Vector2.Zero;
            if (!Enabled) return false;
            foreach (Step s in Main)
            {
                if (Done(s)) continue;
                Vector2? at = Where(s);
                if (!at.HasValue) continue;      // not in this level: skip rather than stall
                text = Describe(s);
                map = at.Value;
                return true;
            }
            return false;
        }

        /// <summary>Optional objectives open now: their main step done, themselves not.</summary>
        public static IEnumerable<KeyValuePair<string, Vector2>> OptionalOpen()
        {
            if (Current != Mode.All) yield break;
            foreach (Step s in Optional)
            {
                if (s.After != null && !OpenedBy(s.After)) continue;
                if (Done(s)) continue;
                Vector2? at = Where(s);
                if (at.HasValue) yield return new KeyValuePair<string, Vector2>(Describe(s), at.Value);
            }
        }

        /// <summary>
        /// The SSMap markers the current objectives point at (the boss, brand, item,
        /// sanctuary, or the NPC at their current spot): what stays on a far-out map.
        /// </summary>
        public static HashSet<Marker> RingedMarkers()
        {
            HashSet<Marker> set = new HashSet<Marker>();
            if (!Enabled) return set;
            Step next = null;
            foreach (Step s in Main)
                if (!Done(s) && Where(s).HasValue) { next = s; break; }
            if (next != null) AddMarkers(set, next);
            if (Current == Mode.All)
                foreach (Step s in Optional)
                    if ((s.After == null || OpenedBy(s.After)) && !Done(s) && Where(s).HasValue) AddMarkers(set, s);
            return set;
        }

        static void AddMarkers(HashSet<Marker> set, Step s)
        {
            switch (s.Kind)
            {
                case Kind.Boss:
                    foreach (Marker m in Markers.All)
                        if (m.Kind == Show.Bosses && Progress.LinkedFlag(m) == s.Key) set.Add(m);
                    break;
                case Kind.Brand:
                    foreach (Marker m in Markers.All)
                        if (Progress.LinkedRune(m) == s.Rune) set.Add(m);
                    break;
                case Kind.Story:
                    {
                        Marker m = Places.MarkerAtCurrentSpot(s.Npc);
                        if (m != null) set.Add(m);
                        break;
                    }
                case Kind.Item:
                case Kind.Sanctuary:
                    {
                        Marker m = Markers.Find(s.Marker);
                        if (m != null) set.Add(m);
                        break;
                    }
            }
        }

        /// <summary>Whether the main step with this boss flag or NPC is done.</summary>
        static bool OpenedBy(string key)
        {
            foreach (Step m in Main)
                if (m.Key == key || m.Npc == key) return Done(m);
            return false;
        }

        /// <summary>For the diagnostic snapshot: every step, whether done, and where it points.</summary>
        public static IEnumerable<string> Report()
        {
            yield return "guide mode: " + Describe();
            foreach (Step s in Main)
            {
                Vector2? at = Where(s);
                yield return "main      " + (Done(s) ? "done " : "open ") + Describe(s) + (at.HasValue ? "" : "  [no target]");
            }
            foreach (Step s in Optional)
            {
                Vector2? at = Where(s);
                string state = Done(s) ? "done " : (s.After != null && !OpenedBy(s.After)) ? "later" : "open ";
                yield return "optional  " + state + " " + Describe(s) + (at.HasValue ? "" : "  [no target]");
            }
        }

        /// <summary>Main route steps done so far, of all of them.</summary>
        public static void Count(out int done, out int total)
        {
            done = 0;
            total = Main.Length;
            foreach (Step s in Main)
                if (Done(s)) done++;
        }
    }
}
