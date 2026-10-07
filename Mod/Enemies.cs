using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonsterEdit.monsters;
using ProjectTower.character;
using ProjectTower.map;

namespace SaltMap
{
    /// <summary>
    /// Where hostile monsters are, refreshed once per frame for the map's red dots.
    /// Live ones come from CharMgr.character; ones far from the camera wait unspawned
    /// in MapMgr.reserveEntities and are listed too, dimmer. Killed enemies leave both
    /// lists until the world respawns them (resting, dying, fast travel).
    /// </summary>
    internal static class Enemies
    {
        public struct Dot
        {
            public Vector2 Map;     // map pixels
            public bool Live;
            public bool Boss;
        }

        public static readonly List<Dot> Dots = new List<Dot>();

        const int TypeMonster = 1;   // MonsterDef.type: 0 NPC, 1 monster, 2 chest, ... 8 mimic

        public static void Update()
        {
            Dots.Clear();
            MonsterDef[] catalog = MonsterCatalog.catalog;
            if (catalog == null) return;

            Character[] chars = CharMgr.character;
            if (chars != null)
            {
                foreach (Character c in chars)
                {
                    if (c == null || !c.exists || c.playerIdx != -1 || c.hp <= 0f || c.dyingFrame > 0f) continue;
                    if (!IsMonster(catalog, c.monsterIdx)) continue;
                    if (c.npcIdx != -1 && !c.npcFightMode) continue;   // a friendly NPC
                    Dots.Add(new Dot { Map = MapSpace.FromWorld(c.loc), Live = true, Boss = c.boss });
                }
            }

            ReserveEntities reserve = MapMgr.reserveEntities;
            if (reserve != null && reserve.reserveChar != null)
            {
                ReserveEntities.ReserveChar[] rs = reserve.reserveChar;
                for (int i = 0; i < rs.Length; i++)
                {
                    if (!rs[i].exists || rs[i].npcIdx != -1 || !IsMonster(catalog, rs[i].monsterIdx)) continue;
                    Dots.Add(new Dot { Map = MapSpace.FromWorld(rs[i].loc), Live = false, Boss = rs[i].boss });
                }
            }
        }

        static bool IsMonster(MonsterDef[] catalog, int idx)
        {
            return idx >= 0 && idx < catalog.Length && catalog[idx] != null && catalog[idx].type == TypeMonster;
        }
    }
}
