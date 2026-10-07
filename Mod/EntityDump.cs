using System;
using System.Globalization;
using System.IO;
using System.Text;
using MapEdit.map;
using MonsterEdit.monsters;
using ProjectTower.map;

namespace SaltMap
{
    /// <summary>
    /// Diagnostic: with "dumpEntities=on" in SaltMap.ini, writes every level entity
    /// (MapMgr.map.layer[19]) to Mods\entities.tsv once the level is loaded, for
    /// working out offline which game object each SSMap marker stands for.
    /// </summary>
    internal static class EntityDump
    {
        static bool done;

        public static void TryWrite(int gameState)
        {
            if (done || !Settings.GetBool("dumpEntities", false)) return;
            Map map = MapMgr.map;
            if (map == null || map.layer == null || map.layer.Length <= 19 || map.layer[19] == null || map.layer[19].seg == null) return;
            done = true;

            Seg[] segs = map.layer[19].seg;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("k\ttexture\ttype\tflags\tx\ty\tscript");
            int n = 0;
            for (int k = 0; k < segs.Length; k++)
            {
                Seg s = segs[k];
                if (s == null) continue;
                int idx = -1, type = -1, flags = -1;
                try
                {
                    idx = MonsterCatalog.GetIdxFromString(s.texture);
                    if (idx >= 0 && MonsterCatalog.catalog != null && idx < MonsterCatalog.catalog.Length && MonsterCatalog.catalog[idx] != null)
                    {
                        type = MonsterCatalog.catalog[idx].type;
                        flags = MonsterCatalog.catalog[idx].flags;
                    }
                }
                catch (Exception) { }
                string script = (s.strFlag ?? "").Replace("\r", "").Replace("\n", " | ").Replace("\t", " ");
                sb.Append(k).Append('\t').Append(s.texture).Append('\t').Append(type).Append('\t').Append(flags).Append('\t')
                  .Append(s.loc.X.ToString("F0", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.loc.Y.ToString("F0", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(script).AppendLine();
                n++;
            }
            string path = Path.Combine(Entry.ModDir, "entities.tsv");
            File.WriteAllText(path, sb.ToString());
            Log.Info("entity dump: " + n + " entities in game state " + gameState + " to " + path);
        }
    }
}
