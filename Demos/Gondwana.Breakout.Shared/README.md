# Gondwana Breakout

Classic brick-breaker demo implemented once in Gondwana.Breakout.Shared and hosted with GPU rendering on WinForms, Avalonia, and Blazor/WebGL.

All three hosts share one procedural SkiaSharp tilesheet, a 32-by-18 logical 60px scene grid (1920 x 1080), the game simulation, controls, and HUD. No external artwork is required.

## Run from the repository root

- WinForms (Windows, .NET 8): dotnet run --project Demos/Gondwana.Breakout.WinForms
- Avalonia (Windows/macOS/Linux, .NET 8): dotnet run --project Demos/Gondwana.Breakout.Avalonia
- Blazor (browser, repository-pinned .NET 8 SDK with matching wasm-tools): dotnet run --project Demos/Gondwana.Breakout.Blazor

## Controls

- Left / Right arrows or A / D: move paddle
- Space: launch the ball
- P: pause / resume
- R: restart with three lives
- Escape: exit on desktop

Desktop windows open with approximately 1280 x 720 client presentation and are resizable/maximizable. The default 1.5x render scale establishes a 1920 x 1080 logical backbuffer. Later window resizing changes presentation rather than game coordinates; differing aspect ratios produce letterboxing. The Avalonia host adjusts initial dimensions for window chrome. Blazor calls ConfigureLogicalResolution(1920, 1080) and uses aspect-fitting CSS. This in-repository example targets net8.0-browser, matching Gondwana.sln and global.json; the separately distributed Gondwana Blazor template targets .NET 10.

The game includes 72 bricks, tiered scores, three lives, angle-controlled paddle rebounds, win/loss states, and collision substeps.
