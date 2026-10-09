using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonsterEdit.monsters;
using ProjectTower.character;
using ProjectTower.map;

namespace SaltMap
{
    /// <summary>
    /// Where hostile monsters are, refreshed once per frame for the map's dots. Live ones
    /// come from CharMgr.character; ones far from the camera wait unspawned in
    /// MapMgr.reserveEntities and are listed too (the views show only those near the
    /// player). Killed and dying enemies are left out until the world respawns them
    /// (resting, dying, fast travel).
    /// </summary>
    internal static class Enemies
    {
        public struct Dot
        {
            public Vector2 Map;      // map pixels, at the feet
            public bool Live;
            public bool Boss;
            public bool Aggro;       // has noticed the player (Character.aggrod), for the hover label
            public int MonsterIdx;
            public float Hp, MaxHp;  // live only, for the hover label
        }

        public static readonly List<Dot> Dots = new List<Dot>();

        // The prologue's boat sits in the level's top-left corner (its sailors and the
        // Unspeakable Deep), where SSMap's map shows only sky. Nothing else is up there.
        static bool OnTheBoat(Vector2 world) { return world.X < 9000f && world.Y < 6000f; }

        const int TypeMonster = 1;     // MonsterDef.type: 0 NPC, 1 monster, 2 chest, ... 8 mimic

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
                    if (c == null || !c.exists || c.playerIdx != -1 || !IsMonster(catalog, c.monsterIdx) || OnTheBoat(c.loc)) continue;
                    if (c.npcIdx != -1 && !c.npcFightMode) continue;   // a friendly NPC
                    if (c.hp <= 0f || c.dyingFrame > 0f) continue;
                    float max = 0f;
                    try { if (c.stats != null) max = c.stats.GetMaxHP(); } catch (Exception) { }
                    Dots.Add(new Dot
                    {
                        Map = MapSpace.FromWorld(c.loc),
                        Live = true,
                        Boss = c.boss,
                        Aggro = c.aggrod,
                        MonsterIdx = c.monsterIdx,
                        Hp = c.hp,
                        MaxHp = max,
                    });
                }
            }

            ReserveEntities reserve = MapMgr.reserveEntities;
            if (reserve != null && reserve.reserveChar != null)
            {
                ReserveEntities.ReserveChar[] rs = reserve.reserveChar;
                for (int i = 0; i < rs.Length; i++)
                {
                    if (!rs[i].exists || rs[i].npcIdx != -1 || !IsMonster(catalog, rs[i].monsterIdx) || OnTheBoat(rs[i].loc)) continue;
                    Dots.Add(new Dot
                    {
                        Map = MapSpace.FromWorld(rs[i].loc),
                        Live = false,
                        Boss = rs[i].boss,
                        MonsterIdx = rs[i].monsterIdx,
                    });
                }
            }
        }

        /// <summary>The monster's name in the game's language, or null.</summary>
        public static string NameOf(int monsterIdx)
        {
            MonsterDef[] catalog = MonsterCatalog.catalog;
            if (catalog == null || monsterIdx < 0 || monsterIdx >= catalog.Length || catalog[monsterIdx] == null) return null;
            string[] t = catalog[monsterIdx].title;
            return t != null && t.Length > 0 ? t[0] : null;
        }

        static bool IsMonster(MonsterDef[] catalog, int idx)
        {
            return idx >= 0 && idx < catalog.Length && catalog[idx] != null && catalog[idx].type == TypeMonster;
        }
    }
}
