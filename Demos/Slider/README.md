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

Rows and columns can each be set from **3 to 20**. The board's size is chosen to preserve the source picture's aspect ratio, and the GPU camera automatically fits it when the window is resized. The status panel shows correctly positioned pieces, player move count, elapsed solving time, and completion.

Shuffle makes legal moves instantly; deliberate player moves are animated. Moves are disabled during their animation, so rapid clicks cannot race with the board update. Shuffle resets the move counter and timer; the victory sound plays only when the player solves the puzzle.

## Architecture

- `SlidingPuzzle.cs`: independent, testable board rules and legal-move shuffle. The logical board, **not sprite coordinates**, is authoritative.
- `PuzzleRenderer.cs`: builds two SkiaSharp tilesheet variants (outlined and plain) and projects board state into GPU-rendered sprites.
- `SliderGameHost.cs`: `WinFormsGpuGameHost` lifecycle, input, scripted animation, audio, timing, and background-thread request processing.
- `GameWindow.cs`: resizable WinForms UI, file dialog, puzzle settings and marshalled status updates.

The host starts **once**. Changing pictures, shuffling, or resizing updates live game state rather than starting or stopping the engine again. External UI commands are queued and processed by the engine thread, while status updates are marshalled to the form.

Pure logic regression tests live in `Testing/Gondwana.Tests/Demos/SlidingPuzzleTests.cs`.

## Follow-ups

This modernization addresses the input/state coupling behind the old freezing risk described in [#32](https://github.com/Isthimius/Gondwana/issues/32), but manual reproduction and Windows GPU smoke testing are still needed before treating it as fixed. High scores, persisted games, a loading progress indicator, a dedicated icon, and coordinate diagnostic overlays remain tracked in [#33](https://github.com/Isthimius/Gondwana/issues/33).
