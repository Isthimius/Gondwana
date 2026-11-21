# Slider

A sliding-picture puzzle built on Gondwana's **GPU WinForms host**. Start with the included procedural scene, or choose a PNG, JPEG, BMP, GIF, or WebP picture to turn it into your own puzzle.

## Run

From the repository root on Windows:

```powershell
dotnet run --project Demos/Slider/Slider.csproj
```

Requires the .NET 10 SDK and the repository's normal Gondwana native GPU dependencies.

## Controls

| Control | Action |
| --- | --- |
| Click a piece adjacent to the empty square | Slide that piece into the opening |
| Arrow keys | Slide a neighboring piece in the arrow's direction |
| Shuffle | Make legal random moves to create a solvable puzzle |
| Open picture | Load a new image without restarting the engine |
| New puzzle | Start over with the currently selected picture and row/column values |
| Show piece outlines | Toggle per-piece boundaries |
| Hold Shift | Show each piece's solved column,row (for example `2,0`), regardless of where the piece is now |
| Game menu | New Puzzle, Open Picture, Shuffle, Save Puzzle, Load Puzzle, Exit |
| Scores menu | View the ten fastest completed games for each board size |
| Help menu | How to Play and Gondwana About Box |
| Escape | Exit when no menu or dialog has focus |

Rows and columns can each be set from **3 to 20**. The image preserves its aspect ratio, and the GPU camera automatically **sizes the complete puzzle to fit** the available panel. Slider explicitly resizes its logical GPU surface to match the panel when the window changes size, instead of keeping a fixed-aspect backbuffer that would introduce external letterboxing or clip the board. Remaining space around a differently shaped image uses the puzzle background color. The status panel shows correctly positioned pieces, player move count, elapsed solving time, and completion. A Gondwana.Widgets progress bar appears while loading a picture, and the application uses the Gondwana window icon.

Shuffle visibly animates legal moves one at a time, faster than normal player slides. Input is disabled during shuffling and while a piece is moving, so rapid clicks cannot race with board updates. The sliding sound starts with each moving piece and stops on arrival or cancellation. Shuffle resets the move counter and timer; the victory sound plays only when the player solves the puzzle. Completing a puzzle records a persistent high score (elapsed time and moves), ranked by board dimensions. New puzzles and Shuffle reset the current attempt's counters; existing high scores are preserved.

The **Game** menu saves portable `.slider.json` files containing the tile arrangement, source picture, move count, and elapsed timer; **Load Puzzle** restores all of them and resumes an unfinished timer. High scores live in the current Windows user's Local Application Data directory under `Gondwana/Slider/high-scores.json`. The **Help** menu shows a scrollable How to Play dialog and the built-in Gondwana.Widgets AboutBox with a Gondwana logo.

## Architecture

- `SlidingPuzzle.cs`: independent, testable board rules and legal-move shuffle. The logical board, **not sprite coordinates**, is authoritative.
- `PuzzleRenderer.cs`: builds two SkiaSharp tilesheet variants (outlined and plain) and projects board state into GPU-rendered sprites.
- `SliderGameHost.cs`: `WinFormsGpuGameHost` lifecycle, two non-overlapping Views, puzzle input, scripted animation, audio, timing, and background-thread request processing.
- `SliderSidebar.cs`: **Gondwana.Widgets** controls, settings, progress indicator, and status.
- `SliderMenu.cs`: in-engine `MenuBarWidget` Game / Scores / Help menus, standard `AboutBox`, and scrollable `DialogBox` instructions and high scores.
- `SliderPersistence.cs`: JSON save/restore and per-board high-score rankings.
- `SliderGameHost.Persistence.cs`: menu callbacks, native UI boundary events, game state capture, high scores, and correct-position labels.
- `GameWindow.cs`: a minimal WinForms shell containing **only one `WinFormGpuRenderSurfaceControl`**; native file pickers for pictures and saves/loads are invoked on the WinForms UI thread by Widgets actions.

The host starts **once**. Mouse clicks and native Windows single-finger taps share the `WidgetInputRouter.UnhandledPrimaryAction` path so one press moves a piece only once. Changing pictures, shuffling, or resizing updates live game state rather than starting or stopping the engine again. Widget actions remain in the engine; the picture chooser is the sole native dialog and is marshalled to the UI thread. The sidebar is a fixed 240-pixel Gondwana View, while the puzzle View occupies the remaining logical GPU area. After resizing, both Viewports are laid out again and the puzzle camera fits the right-hand viewport. The WinForms form has no labels, text boxes, buttons, checkboxes, or layout panels.

Pure logic and saved-state regression tests live in `Testing/Slider.Tests/SlidingPuzzleTests.cs`. They run in the dedicated `demo-tests` CI job on Slider changes and during nightly integration testing.

## Follow-ups

This modernization addresses all listed implementation items from [#33](https://github.com/Isthimius/Gondwana/issues/33) and removes the duplicate input paths implicated in the intermittent freeze from [#32](https://github.com/Isthimius/Gondwana/issues/32). Reproducing the original intermittent freeze and testing native touch, the GPU adapter, modal dialogs, and file persistence on a real Windows machine are still required before closing #32 as verified.
