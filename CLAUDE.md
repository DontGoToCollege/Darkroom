# Darkroom: notes for Claude

Build, test and release recipes, decisions and gotchas, kept in the repo so every computer has them.

Darkroom (github.com/DontGoToCollege/Darkroom) is a WPF + WebView2 photo editor. Source in `src\Darkroom` (editor page `wwwroot\index.html`, embedded into the exe).

- 2026-11-17: the 2.0 exe edition was found in the owner's `Downloads\Darkroom (1)` (copied from another PC, left untouched there) and brought in. Released v2.0.0 = original exe as found, v2.0.1 = version shown in the title strip + publish.ps1 + `--smoke`.
- 1.0.0/1.0.1 were an older, much simpler single-file HTML version (claude.ai-made); they stay as releases only, removed from the tree.
- 2026-11-17: 2.0 reviewed; 2.0.2 fixed Image size with shape layers, undo memory cap (1 GB, `trimHistory`), async content-aware fill with a `busy` guard, too-big save/export messages, Delete on non-pixel layers, Save on close. 2.0.3 (owner's choice): Slicer moves unsliced layers with the joined pieces. Not done: autosave/recovery, chunked save for >~400 MB projects.
- `src\Darkroom\smoke.js` (embedded) is the self-check scenario: shape + Image size, marquee + content-aware fill. Add a step there for each fixed bug.

**How to apply:** release with `.\publish.ps1` (raise `<Version>` in csproj AND `const VERSION` in index.html; it refuses archived versions and runs `Darkroom.exe --smoke`, which opens off-screen without focus and writes `dist\DarkroomData\smoke.txt`). `-Test` builds to `dist` only. Uses .NET SDK 10 to build the net8 target. See user-testing-preference.
