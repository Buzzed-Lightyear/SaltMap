using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;

namespace SaltMap
{
    /// <summary>
    /// SSMap's tiles (tiles\{z}\{x}\{y}.jpeg, 256 px) as textures. Asking for a tile
    /// that is not ready returns null and queues it; a worker thread decodes the JPEG
    /// and the game thread uploads a few per frame. The newest requests go first, so
    /// a fast-moving view does not wait behind tiles it has already left. Tiles not
    /// drawn for a while are dropped once the cache is full.
    /// </summary>
    internal static class TileCache
    {
        const int MaxTextures = 320;          // about 84 MB of 256x256 RGBA
        const int UploadsPerFrame = 6;
        const int StaleFrames = 30;           // requests not repeated for this long are dropped

        enum State { Queued, Loading, Ready, Missing }

        sealed class Entry
        {
            public State State;
            public Texture2D Texture;
            public Pixels Decoded;
            public int LastWanted;
        }

        static readonly object gate = new object();
        static readonly Dictionary<long, Entry> entries = new Dictionary<long, Entry>();
        static readonly List<long> queue = new List<long>();      // keys waiting for the worker, oldest first
        static readonly List<long> decoded = new List<long>();    // keys with pixels waiting for upload
        static readonly AutoResetEvent wake = new AutoResetEvent(false);

        static string root;
        static Thread worker;
        static volatile int frame;
        static int pumpedFrame = -1;
        static int readyCount;
        static bool firstReported;

        public static bool Available { get; private set; }

        public static void Init(string tilesFolder)
        {
            root = tilesFolder;
            Available = Directory.Exists(Path.Combine(root, "0"));
            Log.Info("tiles: " + root + (Available ? "" : " (not found; the map will be empty)"));
            if (!Available) return;
            worker = new Thread(Work) { IsBackground = true, Name = "SaltMap tiles", Priority = ThreadPriority.BelowNormal };
            worker.Start();
        }

        static long Key(int z, int x, int y) { return ((long)z << 48) | ((long)x << 24) | (uint)y; }

        /// <summary>The tile's texture, or null while it loads or if it does not exist. Game thread only.</summary>
        public static Texture2D Get(int z, int x, int y)
        {
            if (!Available || x < 0 || y < 0 || x >= (1 << z) || y >= (1 << z)) return null;
            long key = Key(z, x, y);
            lock (gate)
            {
                Entry e;
                if (!entries.TryGetValue(key, out e))
                {
                    e = new Entry { State = State.Queued };
                    entries[key] = e;
                    queue.Add(key);
                    wake.Set();
                }
                else if (e.State == State.Queued)
                {
                    // Asked again: move to the back, which the worker takes first.
                    queue.Remove(key);
                    queue.Add(key);
                }
                e.LastWanted = frame;
                return e.State == State.Ready ? e.Texture : null;
            }
        }

        /// <summary>Like Get, but never queues: for drawing a coarser tile under one that is loading.</summary>
        public static Texture2D Peek(int z, int x, int y)
        {
            if (!Available) return null;
            lock (gate)
            {
                Entry e;
                if (entries.TryGetValue(Key(z, x, y), out e) && e.State == State.Ready)
                {
                    e.LastWanted = frame;
                    return e.Texture;
                }
                return null;
            }
        }

        /// <summary>Uploads finished tiles and trims the cache. Call once per frame before drawing tiles.</summary>
        public static void Pump(GraphicsDevice device)
        {
            if (!Available || pumpedFrame == frame) return;
            pumpedFrame = frame;
            List<KeyValuePair<long, Pixels>> uploads = new List<KeyValuePair<long, Pixels>>();
            lock (gate)
            {
                while (decoded.Count > 0 && uploads.Count < UploadsPerFrame)
                {
                    long key = decoded[decoded.Count - 1];
                    decoded.RemoveAt(decoded.Count - 1);
                    Entry e;
                    if (!entries.TryGetValue(key, out e) || e.Decoded == null) continue;
                    uploads.Add(new KeyValuePair<long, Pixels>(key, e.Decoded));
                    e.Decoded = null;
                }
            }
            foreach (KeyValuePair<long, Pixels> u in uploads)
            {
                Stopwatch sw = Stopwatch.StartNew();
                Texture2D tex = u.Value.Upload(device);
                lock (gate)
                {
                    Entry e;
                    if (entries.TryGetValue(u.Key, out e)) { e.Texture = tex; e.State = State.Ready; readyCount++; }
                    else tex.Dispose();
                }
                if (!firstReported)
                {
                    firstReported = true;
                    Log.Info("tiles: first tile uploaded (z" + (u.Key >> 48) + "), " + u.Value.Width + "x" + u.Value.Height
                        + ", upload " + sw.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
                }
            }
            if (readyCount > MaxTextures) Trim();
        }

        /// <summary>Marks the end of a frame, so later requests count as newer.</summary>
        public static void EndFrame() { frame++; }

        static void Trim()
        {
            List<KeyValuePair<long, Entry>> ready = new List<KeyValuePair<long, Entry>>();
            lock (gate)
            {
                foreach (KeyValuePair<long, Entry> kv in entries)
                    if (kv.Value.State == State.Ready && kv.Value.LastWanted < frame - 1) ready.Add(kv);
                ready.Sort((a, b) => a.Value.LastWanted.CompareTo(b.Value.LastWanted));
                int drop = Math.Min(ready.Count, readyCount - MaxTextures * 3 / 4);
                for (int i = 0; i < drop; i++)
                {
                    ready[i].Value.Texture.Dispose();
                    entries.Remove(ready[i].Key);
                    readyCount--;
                }
            }
        }

        static void Work()
        {
            while (true)
            {
                long key = -1;
                lock (gate)
                {
                    // Newest first; forget requests nobody has repeated lately.
                    while (queue.Count > 0)
                    {
                        long k = queue[queue.Count - 1];
                        queue.RemoveAt(queue.Count - 1);
                        Entry e = entries[k];
                        if (frame - e.LastWanted > StaleFrames) { entries.Remove(k); continue; }
                        e.State = State.Loading;
                        key = k;
                        break;
                    }
                }
                if (key < 0) { wake.WaitOne(500); continue; }

                int z = (int)(key >> 48), x = (int)((key >> 24) & 0xFFFFFF), y = (int)(key & 0xFFFFFF);
                string path = Path.Combine(root, z.ToString(), x.ToString(), y + ".jpeg");
                Pixels px = null;
                try
                {
                    if (File.Exists(path)) px = Images.Decode(path);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("tile decode", e);
                }
                lock (gate)
                {
                    Entry e;
                    if (!entries.TryGetValue(key, out e)) continue;
                    if (px == null) { e.State = State.Missing; continue; }
                    e.Decoded = px;
                    decoded.Add(key);
                }
            }
        }
    }
}
