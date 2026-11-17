using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Darkroom
{
    /// <summary>
    /// Everything Darkroom keeps lives in a "DarkroomData" folder next to Darkroom.exe,
    /// so the app stays portable. If that folder can't be written (for example, the exe
    /// sits in Program Files), it falls back to %LOCALAPPDATA%\Darkroom.
    /// </summary>
    internal sealed class Storage
    {
        public sealed class Placement
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool Maximized { get; set; }
        }

        private const int MaxRecent = 12;

        public string DataDir { get; }
        public string ProjectsDir { get; }
        public string WebViewDir { get; }
        public string WebAssetsDir { get; }
        public bool IsPortable { get; }

        private string SettingsPath => Path.Combine(DataDir, "settings.json");
        private string RecentPath => Path.Combine(DataDir, "recent.json");
        private string WindowPath => Path.Combine(DataDir, "window.json");

        public Storage()
        {
            string exeDir = AppContext.BaseDirectory;
            string portable = Path.Combine(exeDir, "DarkroomData");
            if (CanWrite(portable))
            {
                DataDir = portable;
                IsPortable = true;
            }
            else
            {
                DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Darkroom");
                Directory.CreateDirectory(DataDir);
            }

            ProjectsDir = Path.Combine(DataDir, "Projects");
            WebViewDir = Path.Combine(DataDir, "WebView2");
            WebAssetsDir = Path.Combine(DataDir, "App");
            Directory.CreateDirectory(ProjectsDir);
            Directory.CreateDirectory(WebViewDir);
            Directory.CreateDirectory(WebAssetsDir);
        }

        private static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /* ---------- settings (owned by the editor, stored as raw JSON) ---------- */

        public string? ReadSettings()
        {
            try { return File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null; }
            catch { return null; }
        }

        public void WriteSettings(string json) => WriteAtomic(SettingsPath, json);

        /* ---------- recent projects ---------- */

        public List<string> ReadRecent()
        {
            try
            {
                if (File.Exists(RecentPath))
                    return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentPath)) ?? new List<string>();
            }
            catch { }
            return new List<string>();
        }

        public void AddRecent(string path)
        {
            var list = ReadRecent();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            if (list.Count > MaxRecent) list.RemoveRange(MaxRecent, list.Count - MaxRecent);
            WriteAtomic(RecentPath, JsonSerializer.Serialize(list));
        }

        public void RemoveRecent(string path)
        {
            var list = ReadRecent();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            WriteAtomic(RecentPath, JsonSerializer.Serialize(list));
        }

        public object[] RecentInfo()
        {
            return ReadRecent().Select(p =>
            {
                bool exists = File.Exists(p);
                return (object)new
                {
                    path = p,
                    name = Path.GetFileNameWithoutExtension(p),
                    folder = Path.GetDirectoryName(p) ?? "",
                    exists,
                    modified = exists ? File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm") : ""
                };
            }).ToArray();
        }

        /* ---------- window placement ---------- */

        public Placement? ReadPlacement()
        {
            try
            {
                return File.Exists(WindowPath)
                    ? JsonSerializer.Deserialize<Placement>(File.ReadAllText(WindowPath))
                    : null;
            }
            catch { return null; }
        }

        public void WritePlacement(Placement p) => WriteAtomic(WindowPath, JsonSerializer.Serialize(p));

        /* ---------- safe writes: never leave a half-written file behind ---------- */

        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, true);
        }

        public static void WriteAtomic(string path, byte[] bytes)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, true);
        }
    }
}
