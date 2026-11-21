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
| Escape | Exit |

Rows and columns can each be set from **3 to 20**. The image preserves its aspect ratio, and the GPU camera automatically **sizes the complete puzzle to fit** the available panel. Slider explicitly resizes its logical GPU surface to match the panel when the window changes size, instead of keeping a fixed-aspect backbuffer that would introduce external letterboxing or clip the board. Remaining space around a differently shaped image uses the puzzle background color. The status panel shows correctly positioned pieces, player move count, elapsed solving time, and completion.

Shuffle visibly animates legal moves one at a time, faster than normal player slides. Input is disabled during shuffling and while a piece is moving, so rapid clicks cannot race with board updates. The sliding sound starts with each moving piece and stops on arrival or cancellation. Shuffle resets the move counter and timer; the victory sound plays only when the player solves the puzzle.

## Architecture

- `SlidingPuzzle.cs`: independent, testable board rules and legal-move shuffle. The logical board, **not sprite coordinates**, is authoritative.
- `PuzzleRenderer.cs`: builds two SkiaSharp tilesheet variants (outlined and plain) and projects board state into GPU-rendered sprites.
- `SliderGameHost.cs`: `WinFormsGpuGameHost` lifecycle, two non-overlapping Views, puzzle input, scripted animation, audio, timing, and background-thread request processing.
- `SliderSidebar.cs`: **Gondwana.Widgets** `PanelWidget`, `ComboBoxWidget`, `ButtonWidget`, `CheckBoxWidget`, and `LabelWidget` for all persistent controls, settings, and status.
- `GameWindow.cs`: a minimal WinForms shell containing **only one `WinFormGpuRenderSurfaceControl`**; the native `OpenFileDialog` is opened on the WinForms UI thread when requested by the widget UI.

The host starts **once**. Changing pictures, shuffling, or resizing updates live game state rather than starting or stopping the engine again. Widget actions remain in the engine; the picture chooser is the sole native dialog and is marshalled to the UI thread. The sidebar is a fixed 240-pixel Gondwana View, while the puzzle View occupies the remaining logical GPU area. After resizing, both Viewports are laid out again and the puzzle camera fits the right-hand viewport. The WinForms form has no labels, text boxes, buttons, checkboxes, or layout panels.

Pure logic regression tests live in `Testing/Gondwana.Tests/Demos/SlidingPuzzleTests.cs`.

## Follow-ups

This modernization addresses the input/state coupling behind the old freezing risk described in [#32](https://github.com/Isthimius/Gondwana/issues/32), but manual reproduction and Windows GPU smoke testing are still needed before treating it as fixed. High scores, persisted games, a loading progress indicator, a dedicated icon, and coordinate diagnostic overlays remain tracked in [#33](https://github.com/Isthimius/Gondwana/issues/33).
