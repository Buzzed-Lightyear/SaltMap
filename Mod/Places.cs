using System;
using System.Collections.Generic;
using System.Text;
using MapEdit.map;
using Microsoft.Xna.Framework;
using MonsterEdit.monsters;
using ProjectTower.map;
using ProjectTower.player;
using ProjectTower.sanctuary;

namespace SaltMap
{
    /// <summary>
    /// NPCs and sanctuaries on the map.
    ///
    /// NPCs move as the story goes on: the level holds each NPC at several spots, each
    /// gated by flags, and SSMap marks every spot ("Mad Jester - #1", "#2"). Each NPC
    /// marker is tied to its spot, and shown only while the game would place the NPC
    /// there, by the game's own rules (MapMgr.CreateMonster / ReserveChar.InitFromSeg):
    /// every "enabledflag X" must be set; any "disabledflag X" set removes it, and
    /// "disabledflag A&amp;B" removes it only when all parts are set; and an NPC whose
    /// own "flag"/"boss"/"talkphase" tag is set is gone. NPCs cannot be killed.
    ///
    /// Sanctuaries are named the way the fast-travel list names them (the area's name,
    /// AreaCatalog.areaStr) with the creed that holds them in this save
    /// (Sanctuary.creed, names in PlayerStats.strs), instead of SSMap's "Empty Sanctuary".
    /// </summary>
    internal static class Places
    {
        sealed class Gate
        {
            public int Index;                                        // k in layer[19].seg
            public Vector2 Loc;
            public List<string> Enabled = new List<string>();
            public List<string[]> Disabled = new List<string[]>();
            public string Tag;
            public bool TagRemoves;                                  // not a chest, switch or trap
        }

        const float NpcReach = 600f;          // world units
        const float SanctuaryReach = 800f;
        const int TypeNpc = 0;

        static readonly Dictionary<Marker, Gate> npcGate = new Dictionary<Marker, Gate>();
        static readonly Dictionary<Marker, string> npcName = new Dictionary<Marker, string>();
        static readonly Dictionary<Marker, int> sanctuaryOf = new Dictionary<Marker, int>();
        // Every spot of every NPC, by the NPC's texture, for "where is this NPC now".
        static readonly Dictionary<string, List<Gate>> spotsOf = new Dictionary<string, List<Gate>>();

        // Sanctuary index -> AreaCatalog area, as FastTravel.SanctuaryDestination maps it.
        static readonly int[] AreaOfSanctuary =
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 17, 4, 5, 9, 19, 8, 3, 7, 20, 20, 20, 15, 18, 22 };
        const int CreedNameBase = 34;         // PlayerStats.strs[34 + creed]; creed 1-9

        public static void Link(Seg[] segs)
        {
            npcGate.Clear();
            npcName.Clear();
            sanctuaryOf.Clear();
            spotsOf.Clear();

            // The NPCs' spots in the level.
            List<int> npcs = new List<int>();
            for (int k = 0; k < segs.Length; k++)
                if (segs[k] != null && segs[k].texture != "deadprop" && TypeOf(segs[k].texture) == TypeNpc)
                {
                    npcs.Add(k);
                    List<Gate> spots;
                    if (!spotsOf.TryGetValue(segs[k].texture, out spots)) spotsOf[segs[k].texture] = spots = new List<Gate>();
                    spots.Add(GateOf(segs[k], k));
                }

            Sanctuary[] sanctuaries = MapMgr.sanctuaryMgr != null ? MapMgr.sanctuaryMgr.sanctuaries : null;
            int npcLinked = 0, npcMarkers = 0, sanctLinked = 0, sanctMarkers = 0;
            foreach (Marker m in Markers.All)
            {
                Vector2 w = MapSpace.ToWorld(m.Map);
                if (m.Kind == Show.Npcs)
                {
                    npcMarkers++;
                    string baseName = WithoutNumber(m.Name);
                    npcName[m] = baseName;
                    int best = -1;
                    float bestDist = NpcReach;
                    foreach (int k in npcs)
                    {
                        float d = Vector2.Distance(w, segs[k].loc);
                        if (d < bestDist) { best = k; bestDist = d; }
                    }
                    // SSMap puts a few off the edge of its map (the Ship Captain): by name then.
                    if (best < 0)
                    {
                        string want = Normalise(baseName);
                        bestDist = float.MaxValue;
                        foreach (int k in npcs)
                        {
                            float d = Vector2.Distance(w, segs[k].loc);
                            if (d < bestDist && (Normalise(segs[k].texture) == want || Normalise(Title(segs[k].texture)) == want)) { best = k; bestDist = d; }
                        }
                    }
                    if (best >= 0) { npcGate[m] = GateOf(segs[best], best); npcLinked++; }
                }
                else if (m.Kind == Show.Sanctuaries && sanctuaries != null)
                {
                    sanctMarkers++;
                    int best = -1;
                    float bestDist = SanctuaryReach;
                    for (int i = 0; i < sanctuaries.Length; i++)
                    {
                        if (sanctuaries[i] == null) continue;
                        float d = Vector2.Distance(w, sanctuaries[i].loc);
                        if (d < bestDist) { best = i; bestDist = d; }
                    }
                    if (best >= 0) { sanctuaryOf[m] = best; sanctLinked++; }
                }
            }
            Log.Info("places: " + npcLinked + " of " + npcMarkers + " NPC markers tied to NPC spots, "
                + sanctLinked + " of " + sanctMarkers + " sanctuary markers to sanctuaries");
        }

        static Gate GateOf(Seg seg, int k)
        {
            Gate g = new Gate { Index = k, Loc = seg.loc };
            if (!string.IsNullOrEmpty(seg.strFlag))
            {
                foreach (string raw in seg.strFlag.Split('\n'))
                {
                    string[] t = raw.TrimEnd('\r').Split(' ');
                    if (t.Length < 2) continue;
                    switch (t[0])
                    {
                        case "enabledflag": g.Enabled.Add(t[1]); break;
                        case "disabledflag": g.Disabled.Add(t[1].Split('&')); break;
                        case "flag": g.Tag = t[1]; break;
                        case "boss": g.Tag = t[1]; break;
                        case "talkphase": g.Tag = "talkphase " + t[1]; break;
                    }
                }
            }
            int type = TypeOf(seg.texture);
            g.TagRemoves = type != 2 && type != 3 && type != 6;
            return g;
        }

        /// <summary>An NPC marker whose NPC is not at that spot in this save.</summary>
        public static bool Absent(Marker m)
        {
            Gate g;
            return npcGate.TryGetValue(m, out g) && !Open(g);
        }

        /// <summary>Where an NPC stands in this save: the first of its spots whose gate is open.</summary>
        public static bool CurrentSpot(string texture, out Vector2 world)
        {
            world = Vector2.Zero;
            List<Gate> spots;
            if (texture == null || !spotsOf.TryGetValue(texture, out spots)) return false;
            foreach (Gate g in spots)
                if (Open(g)) { world = g.Loc; return true; }
            return false;
        }

        /// <summary>The NPC marker tied to the spot where this NPC stands now, or null.</summary>
        public static Marker MarkerAtCurrentSpot(string texture)
        {
            List<Gate> spots;
            if (texture == null || !spotsOf.TryGetValue(texture, out spots)) return null;
            foreach (Gate g in spots)
            {
                if (!Open(g)) continue;
                foreach (KeyValuePair<Marker, Gate> kv in npcGate)
                    if (kv.Value.Index == g.Index) return kv.Key;
                return null;
            }
            return null;
        }

        /// <summary>Whether this save has visited the sanctuary a marker is tied to.</summary>
        public static bool Visited(Marker m)
        {
            int i;
            Player p = PlayerTracker.MainPlayer;
            if (!sanctuaryOf.TryGetValue(m, out i) || p == null || p.sanctuaryVisitCount == null || i >= p.sanctuaryVisitCount.Length) return false;
            return p.sanctuaryVisitCount[i] > 0;
        }

        /// <summary>The game's placement rule for one spot (see the class comment).</summary>
        static bool Open(Gate g)
        {
            foreach (string f in g.Enabled)
                if (!Progress.Has(f)) return false;
            foreach (string[] parts in g.Disabled)
            {
                bool all = true;
                foreach (string f in parts)
                    if (!Progress.Has(f)) { all = false; break; }
                if (all) return false;
            }
            return !(g.TagRemoves && Progress.Has(g.Tag));
        }

        /// <summary>The name to show, or null to keep SSMap's.</summary>
        public static string NameOf(Marker m)
        {
            int i;
            if (sanctuaryOf.TryGetValue(m, out i)) return SanctuaryName(i);
            string n;
            return npcName.TryGetValue(m, out n) ? n : null;
        }

        /// <summary>The icon to draw, or null to keep SSMap's: a claimed sanctuary shows a creed.</summary>
        public static string IconOf(Marker m)
        {
            int i;
            return sanctuaryOf.TryGetValue(m, out i) && Creed(i) > 0 ? "creed.png" : null;
        }

        /// <summary>Sanctuary markers tied to a sanctuary, and how many of those hold a creed.</summary>
        public static void CountSanctuaries(out int claimed, out int total)
        {
            claimed = 0;
            total = sanctuaryOf.Count;
            foreach (int i in sanctuaryOf.Values)
                if (Creed(i) > 0) claimed++;
        }

        /// <summary>For the diagnostic snapshot.</summary>
        public static string LinkOf(Marker m)
        {
            Gate g;
            if (npcGate.TryGetValue(m, out g)) return "NPC spot " + g.Index;
            int i;
            return sanctuaryOf.TryGetValue(m, out i) ? "sanctuary " + i + " (creed " + Creed(i) + ")" : null;
        }

        static int Creed(int i)
        {
            Sanctuary[] s = MapMgr.sanctuaryMgr != null ? MapMgr.sanctuaryMgr.sanctuaries : null;
            return s != null && i < s.Length && s[i] != null ? s[i].creed : 0;
        }

        static string SanctuaryName(int i)
        {
            string area = null;
            StringBuilder[] areas = AreaCatalog.areaStr;
            if (i < AreaOfSanctuary.Length && areas != null && AreaOfSanctuary[i] < areas.Length && areas[AreaOfSanctuary[i]] != null)
                area = areas[AreaOfSanctuary[i]].ToString().Trim();
            int creed = Creed(i);
            string owner = null;
            StringBuilder[] strs = PlayerStats.strs;
            if (creed > 0 && strs != null && CreedNameBase + creed < strs.Length && strs[CreedNameBase + creed] != null)
                owner = strs[CreedNameBase + creed].ToString().Trim();
            return (string.IsNullOrEmpty(area) ? "Sanctuary" : area) + ": " + (owner ?? "empty sanctuary");
        }

        /// <summary>"Mad Jester - #1" -> "Mad Jester": only the NPC's current spot is shown.</summary>
        static string WithoutNumber(string s)
        {
            int at = s.LastIndexOf(" - #", StringComparison.Ordinal);
            return at > 0 ? s.Substring(0, at) : s;
        }

        static int TypeOf(string monster)
        {
            try
            {
                int idx = MonsterCatalog.GetIdxFromString(monster);
                MonsterDef[] cat = MonsterCatalog.catalog;
                return cat != null && idx >= 0 && idx < cat.Length && cat[idx] != null ? cat[idx].type : -1;
            }
            catch (Exception) { return -1; }
        }

        static string Title(string monster)
        {
            try
            {
                int idx = MonsterCatalog.GetIdxFromString(monster);
                MonsterDef[] cat = MonsterCatalog.catalog;
                if (cat == null || idx < 0 || idx >= cat.Length || cat[idx] == null || cat[idx].title == null || cat[idx].title.Length == 0) return "";
                return cat[idx].title[0] ?? "";
            }
            catch (Exception) { return ""; }
        }

        static string Normalise(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
