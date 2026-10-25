# Darkroom

A simple photo editor that runs in your web browser. It's one file: no install, no account, and your photos never leave your computer.

## What it does

- Open a photo or start on a blank canvas (you can also drag, drop or paste an image in)
- Layers: add, duplicate, reorder, hide, rename, set opacity and blend mode
- Brush, eraser, paint bucket and color picker (Alt-click)
- Text in several fonts, with bold, italic and alignment
- Crop, or drag the crop edges outward to make the canvas bigger
- Resize the whole image
- Undo and redo
- Export as PNG or JPEG
- Works on phones and tablets too (pinch to zoom)

## Download and use

1. Go to the [latest release](https://github.com/DontGoToCollege/Darkroom/releases/latest).
2. Download `Darkroom.html`.
3. Double-click it. It opens in your web browser and is ready to use.

Fonts load from Google Fonts when you're online. Offline, Darkroom still works with your computer's built-in fonts.

## Keyboard shortcuts

| Key | Tool |
|---|---|
| V | Move |
| B | Brush |
| E | Eraser |
| G | Paint bucket |
| T | Text |
| C | Crop |
| H | Hand (or hold Space with any tool) |
| [ and ] | Smaller or bigger brush |
| Ctrl+Z / Ctrl+Shift+Z | Undo / redo |
| Ctrl+0 | Fit to screen |

## For developers

The source is `src/darkroom.html`. To make a release, raise `VERSION` in that file, then run `py build.py` (Windows) or `python3 build.py`. It writes `Darkroom.html` and keeps a copy of every version in `Versions/Archive`.

## License

MIT. Anyone can use it.
