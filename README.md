# Darkroom

A portable photo editor for Windows: layers, transform handles, selections (marquee, lasso, magic wand), content-aware fill, clone stamp and healing brush, adjustments and filters, a slicer that cuts out strips and joins the pieces, shapes, on-canvas text with effects, paint bucket, blend tool, snapping, multiple open projects, and saved `.drk` project files.

## Download and use

1. Go to the [latest release](https://github.com/DontGoToCollege/Darkroom/releases/latest).
2. Download `Darkroom.exe`.
3. Put it anywhere (a Tools folder, a USB stick) and double-click it. Nothing to install.

It needs the Microsoft Edge WebView2 Runtime, which comes with Windows 11 and up-to-date Windows 10.

## Using it portably

On first run Darkroom creates a `DarkroomData` folder next to itself:

| Folder / file | What's in it |
|---|---|
| `Projects\` | Default location for saved `.drk` projects |
| `settings.json` | Tool colors, sizes, pinned panels, snapping, theme |
| `recent.json` | Recent projects shown on the start screen |
| `window.json` | Window size and position |
| `WebView2\`, `App\` | Editor cache. Safe to delete. |

If the exe sits somewhere it can't write, like Program Files, data goes to `%LOCALAPPDATA%\Darkroom` instead.

## Settings and hotkeys

The gear at the top right opens Settings, grouped like the ribbon (File, Home, Select, Adjust, Image,
View, Tools) plus **Hotkeys**. Every command can be given any key combination, or have its keys removed.
Taking a key that's already in use asks before moving it. **Darkroom hotkeys take precedence** makes
Darkroom's hotkeys win over Windows shortcuts and programs like AutoHotkey while the Darkroom window is
active. Windows never lets any app take over Ctrl+Alt+Delete or Win+L.

The table below lists the default hotkeys.

## Shortcuts

| Keys | Action |
|---|---|
| Ctrl+S / Alt+S | Save project |
| Ctrl+Shift+S | Export image (PNG, JPEG or WebP, with quality) |
| Ctrl+Alt+S | Save as |
| Ctrl+N / Ctrl+O / Ctrl+W | New / open / close project |
| Ctrl+Tab | Next project |
| Alt+L | Open the layers panel (when it's unpinned) |
| Ctrl+F1 | Pin or unpin the ribbon |
| V M L W | Move, Marquee, Lasso, Magic wand |
| B E G R | Brush, Eraser, Paint bucket, Blend |
| T U X C H | Text, Shapes, Slicer, Crop, Hand |
| S | Toggle snapping |
| Ctrl+T | Transform (resize / rotate handles) |
| Double-click a handle | Stretch that side (or corner) out to the canvas edge |
| Double-click the round knob | Straighten (angle back to 0°) |
| Ctrl+double-click an image | Fit it inside the canvas without stretching |
| Ctrl+Shift+double-click an image | Resize the canvas to that image |
| Alt-click (Clone stamp / Healing brush) | Pick the spot to copy from, then paint |
| Slicer drag / Ctrl+drag | Cut out a vertical / horizontal strip and join the pieces |
| Slicer magnifier | 8× zoom around the cursor with the exact cut line and pixel position. Size 0–200% in the options bar, or [ and ] |
| Scroll over a size box or slider | Change it by 1 (Ctrl+scroll: by 5) |
| Enter / Esc | Apply / cancel a transform |
| Delete | Erase selected pixels, or delete layers when nothing is selected |
| Ctrl+D / Ctrl+Shift+I | Deselect / invert selection |
| Ctrl+A | Select all layers (Move tool) or the whole canvas (selection tools) |
| Ctrl+C / Ctrl+X / Ctrl+V | Copy / cut / paste as a new layer |
| Ctrl+J / Ctrl+Shift+J | Selection to a new layer (copy / cut); duplicates layers with no selection |
| Alt+Backspace | Fill the selection (white, black or any color; Enter repeats your last choice) |
| Ctrl or Shift / Alt while selecting | Add to / subtract from the selection |
| Esc (selection tools) | Cancel the box you're drawing, or clear the selections made with that tool |
| Middle-click a selection | Remove that one selection (any tool); Ctrl+middle-click removes all |
| Middle-drag | Pan the canvas from any tool |
| Shift while resizing | Toggle "keep proportions" |
| Shift while rotating or drawing shapes | Snap to 15° / squares, circles, 45° lines |
| Alt (hold) | Paint bucket: switch Fill and Pick color. Move: skip snapping. Resize: from center |
| Esc | Finish typing text; cancel crop and switch to the Hand tool |
| [ and ] | Brush, eraser and blend size; text size with the Text tool |
| Ctrl+[ / Ctrl+] | Text size while typing |
| Right-click | Selection and layer actions (layer via copy/cut, feather, stroke, inverse…) |
| Space (hold) | Pan |
| Drag a text box border | Move text without switching to the Move tool |
| Ctrl+scroll, Ctrl+0, Ctrl+1 | Zoom, fit, 100% |
| Ctrl+2 / Ctrl+3 | Fit height / fit width |

## For developers

It's a .NET 8 WPF app. The editing canvas runs inside Microsoft Edge WebView2, and the WPF shell handles files, dialogs, settings and window state.

    src/Darkroom/Darkroom.csproj       .NET 8 WPF, self-contained single-file publish settings
    src/Darkroom/App.xaml(.cs)         App startup and crash handler
    src/Darkroom/MainWindow.xaml(.cs)  Hosts the editor and handles save/open/export dialogs
    src/Darkroom/Storage.cs            Portable data folder, settings, recent files
    src/Darkroom/KeyboardHook.cs       "Darkroom hotkeys take precedence"
    src/Darkroom/wwwroot/index.html    The editor itself (embedded into the exe)

To make a release, install the .NET SDK, raise `<Version>` in `Darkroom.csproj` and `VERSION` in `wwwroot/index.html`, then run `.\publish.ps1` in PowerShell. It builds `Darkroom.exe`, runs its self-check (`Darkroom.exe --smoke`), and keeps a copy of every version in `Versions\Archive`. `.\publish.ps1 -Test` builds into `dist` without archiving.

The GitHub workflow in `.github/workflows/build.yml` also builds the exe on every push.

Versions 1.0.x were an earlier, simpler single-file HTML version that ran in a browser. They're still on the [Releases](https://github.com/DontGoToCollege/Darkroom/releases) page.

## License

MIT: free for anyone to use, change and share. See [LICENSE](LICENSE).
