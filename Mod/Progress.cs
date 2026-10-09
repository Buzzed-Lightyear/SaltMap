using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LootEdit;
using LootEdit.loot;
using MapEdit.map;
using Microsoft.Xna.Framework;
using MonsterEdit.monsters;
using ProjectTower.map;
using ProjectTower.map.pickups;
using ProjectTower.player;
using DialogEdit.dialog;

namespace SaltMap
{
    /// <summary>
    /// Ties SSMap's item and boss markers to the game objects they stand for, so a
    /// marker can be hidden once the save's flags say it is done. SSMap knows nothing
    /// of the game, so each marker is matched to a level entity (MapMgr.map.layer[19])
    /// or to an NPC's dialog that hands the item out, by position and by item name (the
    /// game's loot catalog turns ids such as "key_green" into titles such as "Green
    /// Key"). Brands are done once the player owns them (Player.runes). Markers that
    /// match nothing stay visible. NPCs and sanctuaries are handled by Places.
    /// </summary>
    internal static class Progress
    {
        enum Kind { Chest, Drop, Boss, Dialog }

        sealed class Source
        {
            public Kind Kind;
            public Vector2 World;
            public string Flag;                 // "flag X" or "boss X"
            public List<string> Items = new List<string>();      // loot ids, counts removed
            public List<string> Names = new List<string>();      // normalised English titles of items (or the boss)
            public HashSet<int> Categories = new HashSet<int>(); // LootCatalog categories of the items
            public int Index;                   // k in layer[19].seg (for a dialog, the NPC's seg)
        }

        // SSMap group -> loot categories its items can be in (LootCatalog.CATEGORY_*:
        // 0 weapon, 1 shield, 2 armor, 3 ring, 4 consumable, 5 magic, 6 keys, 7 materials).
        // Groups not listed (Other) accept anything.
        static readonly Dictionary<string, int[]> GroupCategories = new Dictionary<string, int[]>
        {
            { "Weapons", new[] { 0 } }, { "Shields", new[] { 1 } }, { "Armors", new[] { 2 } },
            { "Rings", new[] { 3 } }, { "Charms", new[] { 3, 4 } }, { "Key Items", new[] { 6 } },
            { "Spells", new[] { 5 } }, { "Prayers", new[] { 5 } }, { "Incantations", new[] { 5 } },
            { "Gray Pearls", new[] { 4, 6, 7 } }, { "Black Pearls", new[] { 4, 6, 7 } },
        };

        // Each matched marker's level flag X ("flag X" or "boss X" in the entity's script,
        // or the flag a dialog sets when it hands the item over).
        static readonly Dictionary<Marker, string> markerFlag = new Dictionary<Marker, string>();
        // Brand markers: the brand's rune index (LootDef.flags of a category-3, type-2 loot).
        static readonly Dictionary<Marker, int> markerRune = new Dictionary<Marker, int>();
        static Player player;

        static Seg[] linkedSegs;
        static HashSet<string> flagSet = new HashSet<string>();
        static List<string> flagList;
        static int flagCount = -1;
        static readonly HashSet<string> lyingFlags = new HashSet<string>();   // flags of items lying on the ground

        // World units. SSMap's markers are hand-placed, usually within 100 of the object.
        const float BossReach = 1200f;          // nearest boss
        const float NamedReach = 800f;          // an object holding an item of the marker's name
        const float FitReach = 250f;            // nearest object holding the right kind of item
        const float AnyReach = 150f;            // nearest object, for groups of any kind ("Other")

        /// <summary>
        /// Hides the marker? Only matched item, spell and boss markers can be. How the
        /// game sets flags (CharCols, CharScript, CharUpdateDeath, Pickup, TriggerMgr):
        /// opening a chest or killing a flagged enemy sets X and drops the item as a
        /// pickup carrying X; picking it up adds X_pkp. A loot bag sets both at once.
        /// A boss sets only X and its items go straight into the inventory. Dropped
        /// items are not saved, so X without a pickup still lying around means the item
        /// is gone. Hence: done when X_pkp is set, or X is set and nothing carrying X lies
        /// on the ground.
        /// </summary>
        public static bool IsDone(Marker m)
        {
            int rune;
            if (markerRune.TryGetValue(m, out rune))
            {
                bool[] runes = player != null ? player.runes : null;
                return runes != null && rune >= 0 && rune < runes.Length && runes[rune];
            }
            string x;
            if (!markerFlag.TryGetValue(m, out x)) return false;
            return flagSet.Contains(x + "_pkp") || (flagSet.Contains(x) && !lyingFlags.Contains(x));
        }

        /// <summary>
        /// Diagnostic snapshot for tracing wrong icons: every marker with its link and the
        /// state of its flags (Mods\marker-status.tsv), and the save's flags
        /// (Mods\save-flags.txt). Written when the map page opens.
        /// </summary>
        public static void WriteSnapshot()
        {
            StringBuilder sb = new StringBuilder("category\tgroup\tname\tshown as\tlink\tflag\tflag set\tpicked up\tlying\thidden\n");
            foreach (Marker m in Markers.All)
            {
                string x;
                bool linked = markerFlag.TryGetValue(m, out x);
                string link;
                linkInfo.TryGetValue(m, out link);
                if (link == null) link = Places.LinkOf(m);
                sb.Append(m.Category).Append('\t').Append(m.Group).Append('\t').Append(m.Name).Append('\t')
                  .Append(NameOf(m)).Append('\t').Append(link ?? "").Append('\t').Append(linked ? x : "").Append('\t')
                  .Append(linked && flagSet.Contains(x) ? "yes" : "").Append('\t')
                  .Append(linked && flagSet.Contains(x + "_pkp") ? "yes" : "").Append('\t')
                  .Append(linked && lyingFlags.Contains(x) ? "yes" : "").Append('\t')
                  .Append(IsHidden(m) ? "yes" : "").Append('\n');
            }
            File.WriteAllText(Path.Combine(Entry.ModDir, "marker-status.tsv"), sb.ToString());
            List<string> flags = new List<string>(flagSet);
            flags.Sort(StringComparer.Ordinal);
            File.WriteAllLines(Path.Combine(Entry.ModDir, "save-flags.txt"), flags.ToArray());
            File.WriteAllLines(Path.Combine(Entry.ModDir, "guide-status.txt"), new List<string>(Guide.Report()).ToArray());
            Log.Info("progress: snapshot written (" + Markers.All.Count + " markers, " + flags.Count + " save flags, guide status)");
        }

        // How each marker was linked ("Chest 123 by name"), for the snapshot.
        static readonly Dictionary<Marker, string> linkInfo = new Dictionary<Marker, string>();

        /// <summary>Whether the player owns the brand with this rune index (Player.runes).</summary>
        public static bool HasRune(int rune)
        {
            bool[] runes = player != null ? player.runes : null;
            return runes != null && rune >= 0 && rune < runes.Length && runes[rune];
        }

        /// <summary>
        /// Completion for the map page: of the markers linked to something (others cannot
        /// be tracked), how many are done. Items include spells and brands.
        /// </summary>
        public static void Count(Show kind, out int done, out int total)
        {
            done = total = 0;
            foreach (Marker m in Markers.All)
            {
                if (m.Kind != kind || !(markerFlag.ContainsKey(m) || markerRune.ContainsKey(m))) continue;
                total++;
                if (IsDone(m)) done++;
            }
        }

        /// <summary>The level flag a marker is linked to, or null.</summary>
        public static string LinkedFlag(Marker m)
        {
            string x;
            return markerFlag.TryGetValue(m, out x) ? x : null;
        }

        /// <summary>The brand (rune index) a marker is linked to, or -1.</summary>
        public static int LinkedRune(Marker m)
        {
            int r;
            return markerRune.TryGetValue(m, out r) ? r : -1;
        }

        /// <summary>Whether the marker is linked to something the save can say is done.</summary>
        public static bool IsTracked(Marker m)
        {
            return markerFlag.ContainsKey(m) || markerRune.ContainsKey(m);
        }

        /// <summary>Whether the save has a flag (Player.flags).</summary>
        public static bool Has(string flag)
        {
            return !string.IsNullOrEmpty(flag) && flagSet.Contains(flag);
        }

        /// <summary>Leave the marker off the map: done, or an NPC who is not there now.</summary>
        public static bool IsHidden(Marker m)
        {
            return IsDone(m) || Places.Absent(m);
        }

        /// <summary>The icon to draw for the marker (a claimed sanctuary differs from SSMap's).</summary>
        public static string IconOf(Marker m)
        {
            return Places.IconOf(m) ?? m.Icon;
        }

        /// <summary>The name to show for the marker.</summary>
        public static string NameOf(Marker m)
        {
            return Places.NameOf(m) ?? m.Name;
        }

        /// <summary>Once per frame: links markers when the level (re)loads and follows the save's flags.</summary>
        public static void Update(Player current)
        {
            player = current;
            Map map = MapMgr.map;
            Seg[] segs = map != null && map.layer != null && map.layer.Length > 19 && map.layer[19] != null ? map.layer[19].seg : null;
            // Loading a save re-reads the level into new Seg objects; link again then.
            if (segs != null && segs != linkedSegs && LootCatalog.category != null)
            {
                linkedSegs = segs;
                try { Link(segs); }
                catch (Exception e) { Log.ErrorOnce("Progress.Link", e); }
                try { Places.Link(segs); }
                catch (Exception e) { Log.ErrorOnce("Places.Link", e); }
                try { Guide.Link(segs); }
                catch (Exception e) { Log.ErrorOnce("Guide.Link", e); }
            }

            List<string> list = current != null ? current.flags : null;
            if (list != flagList || (list != null && list.Count != flagCount))
            {
                flagList = list;
                flagCount = list != null ? list.Count : -1;
                flagSet = list != null ? new HashSet<string>(list) : new HashSet<string>();
            }

            lyingFlags.Clear();
            Pickup[] pickups = MapMgr.pickupMgr != null ? MapMgr.pickupMgr.pickup : null;
            if (pickups != null)
                foreach (Pickup p in pickups)
                    if (p != null && p.exists && p.flag != null) lyingFlags.Add(p.flag);
        }

        static bool Fits(Source s, int[] cats)
        {
            if (cats == null) return true;
            foreach (int c in cats)
                if (s.Categories.Contains(c)) return true;
            return false;
        }

        static void Link(Seg[] segs)
        {
            Dictionary<string, LootInfo> titles = LootTitles();
            runeByName.Clear();
            foreach (LootInfo info in titles.Values)
                if (info.Category == RuneCategory && info.Type == RuneType) runeByName[Normalise(info.Title)] = info.Flags;
            List<Source> sources = Sources(segs, titles);
            markerFlag.Clear();
            markerRune.Clear();
            linkInfo.Clear();

            int matched = 0, byName = 0, considered = 0;
            StringBuilder report = new StringBuilder("marker\tgroup\tdistance\thow\tentity\tflag\titems\n");
            foreach (Marker m in Markers.All)
            {
                bool boss = m.Category == "Enemies";
                if (!boss && m.Category != "Items" && m.Category != "Spells & Prayers") continue;
                considered++;
                Vector2 w = MapSpace.ToWorld(m.Map);
                string name = Normalise(BaseName(m.Name));

                // Brands are handed over by NPCs and kept as items; the game's own test
                // for owning one is Player.runes[LootDef.flags].
                int rune;
                if (!boss && name.Length > 0 && runeByName.TryGetValue(name, out rune))
                {
                    matched++;
                    byName++;
                    markerRune[m] = rune;
                    linkInfo[m] = "brand " + rune;
                    report.Append(m.Name).Append('\t').Append(m.Group).Append("\t\tbrand\t\trune ").Append(rune).Append("\t\n");
                    continue;
                }

                // A wrong link hides an icon that should stay, which is worse than an icon
                // that never hides, so links need either a name or a close, fitting object.
                // Bosses: the nearest boss, else the boss with that title (SSMap puts
                // "The Unspeakable Deep" off the edge of its map).
                // Items: the closest object holding an item of that name, if close or if
                // that item exists only once; else the nearest object close by that holds
                // the right kind of item (armor for an armor marker, and so on).
                int[] cats;
                GroupCategories.TryGetValue(m.Group, out cats);
                Source nearest = null, named = null, fitting = null;
                float nearestDist = float.MaxValue, namedDist = float.MaxValue, fitDist = float.MaxValue;
                int namedCount = 0;
                foreach (Source s in sources)
                {
                    if (boss && s.Kind != Kind.Boss) continue;
                    // For an item, a boss only counts through its drops' names (Green Key).
                    if (!boss && s.Kind == Kind.Boss && !NameMatches(name, s.Names)) continue;
                    // Likewise an NPC's dialog only counts through its items' names.
                    if (s.Kind == Kind.Dialog && (boss || !NameMatches(name, s.Names))) continue;
                    float d = Vector2.Distance(w, s.World);
                    if ((boss || (s.Kind != Kind.Boss && s.Kind != Kind.Dialog)) && d < nearestDist) { nearest = s; nearestDist = d; }
                    if (name.Length > 0 && NameMatches(name, s.Names))
                    {
                        namedCount++;
                        if (d < namedDist) { named = s; namedDist = d; }
                    }
                    if (!boss && (s.Kind == Kind.Chest || s.Kind == Kind.Drop) && d < fitDist && Fits(s, cats)) { fitting = s; fitDist = d; }
                }

                Source pick = null;
                string how = "none";
                if (boss)
                {
                    if (nearest != null && nearestDist <= BossReach) { pick = nearest; how = "nearest"; }
                    else if (named != null && namedCount == 1) { pick = named; how = "name"; }
                }
                else if (named != null && (namedDist <= NamedReach || namedCount == 1)) { pick = named; how = "name"; }
                else if (cats != null && fitting != null && fitDist <= FitReach) { pick = fitting; how = "kind"; }
                else if (cats == null && nearest != null && nearestDist <= AnyReach) { pick = nearest; how = "nearest"; }
                if (how == "name") byName++;

                if (pick != null)
                {
                    matched++;
                    markerFlag[m] = pick.Flag;
                    linkInfo[m] = pick.Kind + " " + pick.Index + " by " + how;
                }
                float dist = pick == null ? nearestDist : Vector2.Distance(w, pick.World);
                report.Append(m.Name).Append('\t').Append(m.Group).Append('\t')
                    .Append(dist.ToString("F0", CultureInfo.InvariantCulture)).Append('\t').Append(how).Append('\t')
                    .Append(pick == null ? "" : pick.Kind + " " + pick.Index).Append('\t')
                    .Append(pick == null ? "" : pick.Flag).Append('\t')
                    .Append(pick == null ? "" : string.Join(",", pick.Items.ToArray())).Append('\n');
            }
            Log.Info("progress: linked " + matched + " of " + considered + " item and boss markers to level objects ("
                + byName + " by item name); " + sources.Count + " candidate objects");
            try { File.WriteAllText(Path.Combine(Entry.ModDir, "marker-links.tsv"), report.ToString()); }
            catch (Exception e) { Log.ErrorOnce("Progress report", e); }
        }

        // Brands are loot in category 3 with type 2; LootDef.flags is the rune index.
        const int RuneCategory = 3, RuneType = 2;
        static readonly Dictionary<string, int> runeByName = new Dictionary<string, int>();

        static List<Source> Sources(Seg[] segs, Dictionary<string, LootInfo> titles)
        {
            List<Source> list = new List<Source>();
            AddDialogSources(list, segs, titles);
            for (int k = 0; k < segs.Length; k++)
            {
                Seg seg = segs[k];
                if (seg == null || string.IsNullOrEmpty(seg.strFlag)) continue;
                string flag = null, bossFlag = null;
                List<string> chest = new List<string>(), drop = new List<string>();
                foreach (string raw in seg.strFlag.Split('\n'))
                {
                    string[] w = raw.Trim().Split(' ');
                    if (w.Length < 2) continue;
                    string item = w[1].Split('/')[0];
                    switch (w[0])
                    {
                        case "flag": flag = w[1]; break;
                        case "boss": bossFlag = w[1]; break;
                        case "chest": chest.Add(item); break;
                        case "drop": drop.Add(item); break;
                    }
                }
                int type = TypeOf(seg.texture);
                Source s = null;
                if (bossFlag != null)
                {
                    s = new Source { Kind = Kind.Boss, Flag = bossFlag };
                    s.Items.AddRange(drop);
                    string title = MonsterTitle(seg.texture);
                    if (title != null) s.Names.Add(Normalise(title));
                }
                else if (type == 2 && flag != null && chest.Count > 0)
                {
                    s = new Source { Kind = Kind.Chest, Flag = flag };
                    s.Items.AddRange(chest);
                }
                else if (flag != null && drop.Count > 0)
                {
                    s = new Source { Kind = Kind.Drop, Flag = flag };
                    s.Items.AddRange(drop);
                }
                if (s == null) continue;
                s.World = seg.loc;
                s.Index = k;
                foreach (string id in s.Items)
                {
                    LootInfo info;
                    if (!titles.TryGetValue(id, out info)) continue;
                    s.Names.Add(Normalise(info.Title));
                    s.Categories.Add(info.Category);
                }
                list.Add(s);
            }
            return list;
        }

        /// <summary>
        /// Items NPCs hand over in dialog (DialogNode.giveScript), with the flag the node
        /// sets (postSetFlagStr), placed at each spot where that NPC stands (the NPC's
        /// segs share the dialog's name as their texture).
        /// </summary>
        static void AddDialogSources(List<Source> list, Seg[] segs, Dictionary<string, LootInfo> titles)
        {
            NPCDialog[] dialogs = DialogMgr.dialogList;
            if (dialogs == null) return;
            foreach (NPCDialog d in dialogs)
            {
                if (d == null || string.IsNullOrEmpty(d.name) || d.nodeList == null) continue;
                foreach (DialogNode n in d.nodeList)
                {
                    if (n == null || n.giveScript == null || n.giveScript.Length == 0 || string.IsNullOrEmpty(n.postSetFlagStr)) continue;
                    for (int k = 0; k < segs.Length; k++)
                    {
                        if (segs[k] == null || segs[k].texture != d.name) continue;
                        Source s = new Source { Kind = Kind.Dialog, Flag = n.postSetFlagStr, World = segs[k].loc, Index = k };
                        foreach (string g in n.giveScript)
                        {
                            if (string.IsNullOrEmpty(g)) continue;
                            string id = g.Trim().Split(' ')[0].Split('/')[0];
                            s.Items.Add(id);
                            LootInfo info;
                            if (!titles.TryGetValue(id, out info)) continue;
                            s.Names.Add(Normalise(info.Title));
                            s.Categories.Add(info.Category);
                        }
                        list.Add(s);
                    }
                }
            }
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

        static string MonsterTitle(string monster)
        {
            try
            {
                int idx = MonsterCatalog.GetIdxFromString(monster);
                MonsterDef[] cat = MonsterCatalog.catalog;
                if (cat == null || idx < 0 || idx >= cat.Length || cat[idx] == null || cat[idx].title == null || cat[idx].title.Length == 0) return null;
                return cat[idx].title[0];
            }
            catch (Exception) { return null; }
        }

        struct LootInfo
        {
            public string Title;
            public int Category;
            public int Type;
            public int Flags;
        }

        /// <summary>Loot id to English title and category, from every loot category.</summary>
        static Dictionary<string, LootInfo> LootTitles()
        {
            Dictionary<string, LootInfo> d = new Dictionary<string, LootInfo>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < LootCatalog.category.Length; c++)
            {
                LootCategory cat = LootCatalog.category[c];
                if (cat == null || cat.loot == null) continue;
                foreach (LootDef l in cat.loot)
                    if (l != null && l.name != null && l.title != null && l.title.Length > 0 && l.title[0] != null && !d.ContainsKey(l.name))
                        d[l.name] = new LootInfo { Title = l.title[0], Category = c, Type = l.type, Flags = l.flags };
            }
            return d;
        }

        /// <summary>SSMap names look like "Woodsman's Axe - Axe 0"; the part before " - " is the item.</summary>
        static string BaseName(string s)
        {
            int dash = s.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? s.Substring(0, dash) : s;
        }

        static string Normalise(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        static bool NameMatches(string name, List<string> names)
        {
            foreach (string n in names)
                if (n.Length > 0 && (n == name || (name.Length >= 6 && n.Contains(name)) || (n.Length >= 6 && name.Contains(n)))) return true;
            return false;
        }
    }
}
