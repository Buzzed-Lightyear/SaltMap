// Entry point. The patched salt.exe calls SaltMap.Entry.Init() once, at the
// start of Game1.Initialize, before the game has loaded any content.
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using ProjectTower;

namespace SaltMap
{
    public static class Entry
    {
        static bool started;

        /// <summary>Folder this DLL was loaded from (the game's Mods folder).</summary>
        public static string ModDir { get; private set; }

        public static void Init()
        {
            if (started) return;
            started = true;

            ModDir = Path.GetDirectoryName(typeof(Entry).Assembly.Location);
            Log.Open(Path.Combine(ModDir, "SaltMap.log"));
            try
            {
                Assembly game = typeof(Game1).Assembly;
                Log.Info("SaltMap " + typeof(Entry).Assembly.GetName().Version + " loading into "
                    + game.GetName().Name + ", file dated "
                    + File.GetLastWriteTime(game.Location).ToString("yyyy-MM-dd HH:mm"));

                // Map data. Any of these may fail (say the SSMap folder is missing) without
                // stopping the rest of the mod; each says so in the log.
                try { Settings.Load(ModDir); }
                catch (Exception e) { Log.Info("settings: failed to read, using defaults: " + e.Message); }
                Minimap.Init();
                try { Markers.Load(Settings.SsMapFolder); }
                catch (Exception e) { Log.Info("markers: failed to load: " + e); }
                try { TileCache.Init(Path.Combine(Settings.SsMapFolder, "tiles")); }
                catch (Exception e) { Log.Info("tiles: failed to start: " + e); }

                // Every class in this assembly marked [HarmonyPatch] is applied here.
                Harmony harmony = new Harmony("saltmap");
                harmony.PatchAll(typeof(Entry).Assembly);
                foreach (MethodBase m in harmony.GetPatchedMethods())
                    Log.Info("hooked " + m.DeclaringType.FullName + "." + m.Name);

                Log.Info("SaltMap ready");
            }
            catch (Exception e)
            {
                Log.Info("SaltMap failed to start: " + e);
                throw;   // the loader in salt.exe records it too and lets the game carry on
            }
        }
    }
}
