using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Darkroom
{
    public partial class MainWindow : Window
    {
        private const string HostName = "darkroom.local";
        private readonly Storage _store = new Storage();
        private int _dirtyCount;
        private string _lastExportDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        // "Darkroom hotkeys take precedence"
        private readonly KeyboardHook _keyHook;
        private HashSet<string> _hotkeys = new HashSet<string>(StringComparer.Ordinal);
        private bool _precedence;
        private bool _typing;
        private IntPtr _hwnd = IntPtr.Zero;
        private static readonly HashSet<string> EditingCombos = new HashSet<string>(StringComparer.Ordinal)
        {
            "Ctrl+A", "Ctrl+C", "Ctrl+V", "Ctrl+X", "Ctrl+Z", "Ctrl+Y", "Ctrl+Shift+Z", "Ctrl+Backspace", "Ctrl+Delete",
            "Ctrl+Left", "Ctrl+Right", "Ctrl+Shift+Left", "Ctrl+Shift+Right", "Ctrl+Home", "Ctrl+End",
            "Ctrl+Shift+Home", "Ctrl+Shift+End", "Alt+Backspace"
        };

        // --smoke: the build's self-check. Opens off-screen without taking focus, checks the editor loaded, writes smoke.txt, exits.
        private readonly bool _smoke = Array.Exists(Environment.GetCommandLineArgs(), a => a.Equals("--smoke", StringComparison.OrdinalIgnoreCase));

        public MainWindow()
        {
            InitializeComponent();
            if (_smoke)
            {
                ShowActivated = false;
                ShowInTaskbar = false;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Top = -32000;
            }
            _keyHook = new KeyboardHook(() => _hwnd, ShouldTakeHotkey, combo => Dispatcher.BeginInvoke(new Action(() => SendHotkey(combo))));
            SourceInitialized += (_, _) => _hwnd = new WindowInteropHelper(this).Handle;
            Activated += (_, _) => { if (_precedence && _hotkeys.Count > 0) _keyHook.Install(); };   // stay first in line
            Closed += (_, _) => _keyHook.Dispose();
            if (!_smoke) RestorePlacement();
            Loaded += async (_, _) => await InitWebViewAsync();
            Closing += OnClosing;
        }

        /* ================= startup ================= */

        private async Task InitWebViewAsync()
        {
            try
            {
                ExtractWebAssets();
                var env = await CoreWebView2Environment.CreateAsync(null, _store.WebViewDir);
                await Web.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex) when (_smoke)
            {
                WriteSmoke(false, "editor didn't start: " + ex.Message);
                return;
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(this,
                    "Darkroom needs the Microsoft Edge WebView2 Runtime. It comes with Windows 11 and up-to-date Windows 10.\n\n" +
                    "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703, then start Darkroom again.",
                    "Darkroom", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Darkroom couldn't start its editor.\n\n" + ex.Message,
                    "Darkroom", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            CoreWebView2 core = Web.CoreWebView2;
            core.Settings.IsZoomControlEnabled = false;              // Ctrl+scroll zooms the canvas, not the UI
            core.Settings.IsPinchZoomEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;  // no F5 reload, Ctrl+P, etc.
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
#if !DEBUG
            core.Settings.AreDevToolsEnabled = false;
#endif
            core.SetVirtualHostNameToFolderMapping(HostName, _store.WebAssetsDir, CoreWebView2HostResourceAccessKind.Allow);
            core.WebMessageReceived += OnWebMessage;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            if (_smoke)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync(
                    "window.__smokeErrors=[];addEventListener('error',e=>__smokeErrors.push(e.message));" +
                    "addEventListener('unhandledrejection',e=>__smokeErrors.push(String(e.reason)));");
                core.NavigationCompleted += async (_, e) => await RunSmokeAsync(e.IsSuccess);
            }
            core.Navigate($"https://{HostName}/index.html");
        }

        private async Task RunSmokeAsync(bool navOk)
        {
            string want = "v" + typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
            string ver = "", errors = "";
            try
            {
                await Task.Delay(2000);   // let the editor load its settings and draw the start screen
                using var js = new StreamReader(typeof(MainWindow).Assembly.GetManifestResourceStream("smoke.js")!);
                string args = JsonSerializer.Serialize(new { expression = js.ReadToEnd(), awaitPromise = true, returnByValue = true });
                using JsonDocument res = JsonDocument.Parse(await Web.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", args));
                if (res.RootElement.TryGetProperty("exceptionDetails", out JsonElement ex)) throw new InvalidOperationException(ex.ToString());
                using JsonDocument doc = JsonDocument.Parse(res.RootElement.GetProperty("result").GetProperty("value").GetString() ?? "{}");
                ver = doc.RootElement.GetProperty("ver").GetString() ?? "";
                foreach (JsonElement el in doc.RootElement.GetProperty("errors").EnumerateArray()) errors += el.GetString() + "; ";
            }
            catch (Exception ex) { errors += "smoke script failed: " + ex.Message; }
            WriteSmoke(navOk && ver == want && errors.Length == 0,
                $"navigation ok: {navOk}\nversion shown: {ver} (want {want})\nerrors: {(errors.Length == 0 ? "none" : errors)}");
        }

        private void WriteSmoke(bool pass, string details)
        {
            File.WriteAllText(Path.Combine(_store.DataDir, "smoke.txt"), (pass ? "PASS" : "FAIL") + "\n" + details + "\n");
            Close();
        }

        /// <summary>Copies the editor page out of the exe so WebView2 can load it.</summary>
        private void ExtractWebAssets()
        {
            using Stream? source = typeof(MainWindow).Assembly.GetManifestResourceStream("Darkroom.index.html");
            if (source == null) throw new InvalidOperationException("The editor files are missing from this build.");
            string target = Path.Combine(_store.WebAssetsDir, "index.html");
            try
            {
                using var file = File.Create(target);
                source.CopyTo(file);
            }
            catch (IOException) when (File.Exists(target))
            {
                // Another Darkroom window is using the file; the copy already there is fine.
            }
        }

        /* ================= messages from the editor ================= */

        private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            int id = 0;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("id", out JsonElement idEl) && idEl.ValueKind == JsonValueKind.Number)
                    id = idEl.GetInt32();
                string cmd = root.TryGetProperty("cmd", out JsonElement c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() ?? "" : "";
                JsonElement args = root.TryGetProperty("args", out JsonElement a) && a.ValueKind == JsonValueKind.Object
                    ? a : default;

                var files = new List<string>();
                if (e.AdditionalObjects != null)
                {
                    foreach (object o in e.AdditionalObjects)
                        if (o is CoreWebView2File f) files.Add(f.Path);
                }

                object? result = Handle(cmd, args, files);
                Reply(id, true, result, null);
            }
            catch (Exception ex)
            {
                Reply(id, false, null, ex.Message);
            }
        }

        private object? Handle(string cmd, JsonElement args, List<string> files)
        {
            switch (cmd)
            {
                case "getSettings":
                    return new
                    {
                        settings = _store.ReadSettings(),
                        recent = _store.RecentInfo(),
                        dataDir = _store.DataDir,
                        projectsDir = _store.ProjectsDir,
                        portable = _store.IsPortable
                    };

                case "setSettings":
                    _store.WriteSettings(Str(args, "json") ?? "{}");
                    return true;

                case "recent":
                    return _store.RecentInfo();

                case "removeRecent":
                {
                    string? p = Str(args, "path");
                    if (p != null) _store.RemoveRecent(p);
                    return _store.RecentInfo();
                }

                case "saveProject":
                    return SaveProject(args);

                case "openProject":
                    return OpenProject(Str(args, "path"));

                case "openDroppedProject":
                    if (files.Count == 0) throw new InvalidOperationException("No file was received.");
                    return OpenProject(files[0]);

                case "pickExportPath":
                    return PickExportPath(args);

                case "writeFile":
                {
                    string path = Str(args, "path") ?? throw new InvalidOperationException("No file path was given.");
                    string b64 = Str(args, "base64") ?? throw new InvalidOperationException("No image data was given.");
                    Storage.WriteAtomic(path, Convert.FromBase64String(b64));
                    return true;
                }

                case "setDirty":
                    _dirtyCount = Int(args, "count");
                    return true;

                case "setTitle":
                    Title = Str(args, "title") ?? "Darkroom";
                    return true;

                case "setHotkeys":
                {
                    var set = new HashSet<string>(StringComparer.Ordinal);
                    if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("combos", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
                        foreach (JsonElement el in arr.EnumerateArray())
                            if (el.ValueKind == JsonValueKind.String && el.GetString() is string k) set.Add(k);
                    _hotkeys = set;
                    _precedence = Bool(args, "precedence");
                    if (_precedence && _hotkeys.Count > 0) _keyHook.Install(); else _keyHook.Uninstall();
                    return true;
                }

                case "setTyping":
                    _typing = Bool(args, "typing");
                    return true;

                case "quit":   // everything was saved after the Save-on-close question
                    _dirtyCount = 0;
                    Dispatcher.BeginInvoke(new Action(Close));
                    return true;

                case "openFolder":
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _store.ProjectsDir + "\"") { UseShellExecute = true });
                    return true;

                default:
                    throw new InvalidOperationException("Unknown command: " + cmd);
            }
        }

        private object? SaveProject(JsonElement args)
        {
            string data = Str(args, "data") ?? throw new InvalidOperationException("There was nothing to save.");
            string? path = Str(args, "path");
            string? folder = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(path) || folder == null || !Directory.Exists(folder))
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Save project",
                    Filter = "Darkroom project (*.drk)|*.drk",
                    DefaultExt = ".drk",
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = SafeName(Str(args, "name")),
                    InitialDirectory = _store.ProjectsDir
                };
                if (dlg.ShowDialog(this) != true) return null;
                path = dlg.FileName;
            }

            Storage.WriteAtomic(path, data);
            _store.AddRecent(path);
            return new { path, name = Path.GetFileNameWithoutExtension(path) };
        }

        private object? OpenProject(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Open project",
                    Filter = "Darkroom project (*.drk)|*.drk|All files (*.*)|*.*",
                    InitialDirectory = _store.ProjectsDir
                };
                if (dlg.ShowDialog(this) != true) return null;
                path = dlg.FileName;
            }

            if (!File.Exists(path))
            {
                _store.RemoveRecent(path);
                throw new FileNotFoundException("That project file can't be found. It may have been moved or deleted.");
            }

            string data = File.ReadAllText(path);
            _store.AddRecent(path);
            return new { path, name = Path.GetFileNameWithoutExtension(path), data };
        }

        private object? PickExportPath(JsonElement args)
        {
            // The format is chosen in Darkroom's own Export dialog; this just asks where to save it.
            string fmt = Str(args, "format") ?? "png";
            var (label, ext, pattern) = fmt switch
            {
                "jpg" => ("JPEG image (*.jpg)", "jpg", "*.jpg;*.jpeg"),
                "webp" => ("WebP image (*.webp)", "webp", "*.webp"),
                _ => ("PNG image (*.png)", "png", "*.png")
            };
            if (ext == "png") fmt = "png";

            var dlg = new SaveFileDialog
            {
                Title = "Export image",
                Filter = $"{label}|{pattern}",
                DefaultExt = "." + ext,
                AddExtension = true,
                OverwritePrompt = true,
                FileName = SafeName(Str(args, "name")) + "." + ext,
                InitialDirectory = Directory.Exists(_lastExportDir) ? _lastExportDir : ""
            };
            if (dlg.ShowDialog(this) != true) return null;

            string path = dlg.FileName;
            string current = Path.GetExtension(path).ToLowerInvariant();
            bool matches = current == "." + ext || (ext == "jpg" && current == ".jpeg");
            if (!matches) path = Path.ChangeExtension(path, ext);

            _lastExportDir = Path.GetDirectoryName(path) ?? _lastExportDir;
            return new { path, format = fmt };
        }

        private void Reply(int id, bool ok, object? result, string? error)
        {
            if (Web.CoreWebView2 == null) return;
            string json = JsonSerializer.Serialize(new { id, ok, result, error });
            Web.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string? Str(JsonElement args, string name) =>
            args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        private static int Int(JsonElement args, string name) =>
            args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) &&
            v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : 0;

        private static bool Bool(JsonElement args, string name) =>
            args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.True;

        /* ================= hotkey precedence ================= */

        private bool ShouldTakeHotkey(string combo)
        {
            if (!_precedence || !_hotkeys.Contains(combo)) return false;
            if (_typing)
            {
                // While typing in a text box, plain keys and normal editing shortcuts stay with the text box.
                bool strong = combo.StartsWith("Ctrl+", StringComparison.Ordinal) || combo.Contains("Alt+");
                if (!strong || EditingCombos.Contains(combo)) return false;
            }
            return true;
        }

        private void SendHotkey(string combo)
        {
            if (Web.CoreWebView2 == null) return;
            Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "hotkey", combo }));
        }

        private static string SafeName(string? name)
        {
            string clean = string.Concat((name ?? "").Split(Path.GetInvalidFileNameChars())).Trim();
            return clean.Length == 0 ? "Untitled" : clean;
        }

        /* ================= closing & window placement ================= */

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_smoke) return;
            if (_dirtyCount > 0)
            {
                string s = _dirtyCount == 1 ? "" : "s";
                var answer = MessageBox.Show(this,
                    $"You have unsaved changes in {_dirtyCount} project{s}. Save them before closing?\n\nYes saves, No closes without saving.",
                    "Darkroom", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
                if (answer == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (answer == MessageBoxResult.Yes)
                {
                    // The editor saves each project (asking where for new ones), then sends "quit".
                    e.Cancel = true;
                    Web.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"saveAll\"}");
                    return;
                }
            }
            SavePlacement();
        }

        private void RestorePlacement()
        {
            Storage.Placement? p = _store.ReadPlacement();
            if (p == null || p.Width < 600 || p.Height < 400) return;

            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            bool onScreen = p.Left + 100 > vl && p.Top + 40 > vt && p.Left + 100 < vl + vw && p.Top + 40 < vt + vh;
            if (!onScreen) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = p.Left;
            Top = p.Top;
            Width = p.Width;
            Height = p.Height;
            if (p.Maximized) WindowState = WindowState.Maximized;
        }

        private void SavePlacement()
        {
            try
            {
                Rect b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
                _store.WritePlacement(new Storage.Placement
                {
                    Left = b.Left,
                    Top = b.Top,
                    Width = b.Width,
                    Height = b.Height,
                    Maximized = WindowState == WindowState.Maximized
                });
            }
            catch
            {
                // Remembering the window size is a nicety; never block closing over it.
            }
        }
    }
}
