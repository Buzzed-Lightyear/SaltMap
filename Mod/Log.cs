using System;
using System.Collections.Generic;
using System.IO;

namespace SaltMap
{
    /// <summary>Writes Mods\SaltMap.log, started fresh on every launch.</summary>
    internal static class Log
    {
        static readonly object gate = new object();
        static readonly HashSet<string> reported = new HashSet<string>();
        static StreamWriter writer;

        public static void Open(string path)
        {
            // Shared for reading, so the log can be watched while the game runs.
            writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
            writer.AutoFlush = true;
        }

        public static void Info(string message)
        {
            lock (gate)
            {
                if (writer != null)
                    writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message);
            }
        }

        /// <summary>
        /// Logs an exception the first time it is seen under this key. Hooks run
        /// every frame, so a repeating failure must not flood the log.
        /// </summary>
        public static void ErrorOnce(string key, Exception e)
        {
            lock (gate)
            {
                if (!reported.Add(key + "|" + e.GetType().FullName + "|" + e.Message)) return;
            }
            Info("ERROR in " + key + ": " + e);
        }
    }
}
