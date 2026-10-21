using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>Where world saves live (12 §3). All calls complete through callbacks (the Web store is async).</summary>
    public interface ISaveStore
    {
        string Name { get; }
        /// <summary>Saves `data[0..length)` (the host's save buffer, reused from save to save: the store copies what it keeps before it returns).</summary>
        void Save(string key, byte[] data, int length, Action<bool> done);
        void Load(string key, Action<byte[]> done);
        void Delete(string key);
    }

    public static class SaveStores
    {
        public static ISaveStore Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebSaveStore();
#else
            return new FileSaveStore(Path.Combine(Application.persistentDataPath, "saves"));
#endif
        }
    }

    /// <summary>Editor/desktop: files, written to a temporary name then moved over (a crash never leaves half a save).</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        readonly string _dir;
        public FileSaveStore(string dir) { _dir = dir; Directory.CreateDirectory(dir); }
        public string Name => "files: " + _dir;

        string PathOf(string key) => Path.Combine(_dir, key + ".tc2");

        public void Save(string key, byte[] data, int length, Action<bool> done)
        {
            try
            {
                string p = PathOf(key), tmp = p + ".tmp";
                using (var f = new FileStream(tmp, FileMode.Create, FileAccess.Write)) f.Write(data, 0, length);
                if (File.Exists(p)) File.Copy(p, p + ".bak", true);
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
                done?.Invoke(true);
            }
            catch (Exception e) { Debug.LogError("[TC2] save failed: " + e.Message); done?.Invoke(false); }
        }

        public void Load(string key, Action<byte[]> done)
        {
            try { string p = PathOf(key); done(File.Exists(p) ? File.ReadAllBytes(p) : null); }
            catch (Exception e) { Debug.LogError("[TC2] load failed: " + e.Message); done(null); }
        }

        public void Delete(string key) { try { File.Delete(PathOf(key)); } catch { } }
    }

    /// <summary>The browser: IndexedDB through tc2.jslib, results delivered by SendMessage to the TC2 object.</summary>
    public sealed class WebSaveStore : ISaveStore
    {
        public string Name => "indexeddb: tiny-consequences-2";
        static readonly Dictionary<string, Action<bool>> _saving = new Dictionary<string, Action<bool>>();
        static readonly Dictionary<string, Action<byte[]>> _loading = new Dictionary<string, Action<byte[]>>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TC2_IdbSave(string key, IntPtr data, int len);
        [DllImport("__Internal")] static extern void TC2_IdbLoad(string key);
        [DllImport("__Internal")] static extern int TC2_IdbTake(string key, IntPtr dest, int len);
        [DllImport("__Internal")] static extern void TC2_IdbDelete(string key);
#else
        static void TC2_IdbSave(string key, IntPtr data, int len) { }
        static void TC2_IdbLoad(string key) { }
        static int TC2_IdbTake(string key, IntPtr dest, int len) => 0;
        static void TC2_IdbDelete(string key) { }
#endif

        public void Save(string key, byte[] data, int length, Action<bool> done)
        {
            _saving[key] = done;
            var h = GCHandle.Alloc(data, GCHandleType.Pinned);
            try { TC2_IdbSave(key, h.AddrOfPinnedObject(), length); }
            finally { h.Free(); } // the jslib copies the bytes synchronously (HEAPU8.slice)
        }

        public void Load(string key, Action<byte[]> done)
        {
            _loading[key] = done;
            TC2_IdbLoad(key);
        }

        public void Delete(string key) => TC2_IdbDelete(key);

        /// <summary>Called by the TC2 object's OnIdbSaved("key|ok").</summary>
        public static void OnSaved(string msg)
        {
            int bar = msg.LastIndexOf('|');
            string key = msg.Substring(0, bar);
            bool ok = msg.Substring(bar + 1) == "ok";
            if (_saving.TryGetValue(key, out var cb)) { _saving.Remove(key); cb?.Invoke(ok); }
        }

        /// <summary>Called by OnIdbLoaded("key|size"); size 0 = nothing saved, -1 = error.</summary>
        public static void OnLoaded(string msg)
        {
            int bar = msg.LastIndexOf('|');
            string key = msg.Substring(0, bar);
            int size = int.Parse(msg.Substring(bar + 1), System.Globalization.CultureInfo.InvariantCulture);
            if (!_loading.TryGetValue(key, out var cb)) return;
            _loading.Remove(key);
            if (size <= 0) { cb(null); return; }
            var data = new byte[size];
            var h = GCHandle.Alloc(data, GCHandleType.Pinned);
            int ok;
            try { ok = TC2_IdbTake(key, h.AddrOfPinnedObject(), size); }
            finally { h.Free(); }
            cb(ok == 1 ? data : null);
        }
    }

    /// <summary>Small browser services (real clock, URL parameters, page events, the test bridge).</summary>
    public static class Browser
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern double TC2_NowMs();
        [DllImport("__Internal")] static extern string TC2_UrlParam(string name);
        [DllImport("__Internal")] public static extern void TC2_RegisterPageEvents();
        [DllImport("__Internal")] public static extern void TC2_PostState(string json);
        [DllImport("__Internal")] public static extern string TC2_PollCommand();
        [DllImport("__Internal")] public static extern void TC2_Log(string msg);
        public static long NowUtcMs() => (long)TC2_NowMs();
        public static string Param(string name) { var v = TC2_UrlParam(name); return string.IsNullOrEmpty(v) ? null : v; }
        public static bool IsWeb => true;
#else
        public static void TC2_RegisterPageEvents() { }
        public static void TC2_PostState(string json) { }
        public static string TC2_PollCommand() => "";
        public static void TC2_Log(string msg) { }
        public static long NowUtcMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static string Param(string name)
        {
            // editor/desktop: command-line "-tc2.name value"
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-tc2." + name) return a[i + 1];
            return null;
        }
        public static bool IsWeb => false;
#endif
    }
}
