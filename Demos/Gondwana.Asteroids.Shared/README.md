# Gondwana Asteroids

An original vector-style tribute to the arcade classic. All gameplay, art, scene setup, HUD, and input actions live in Gondwana.Asteroids.Shared. The three GPU hosts reference the shared project normally; no linked source files or copied game logic.

Run from the repository root (Windows required for WinForms):

```sh
dotnet run --project Demos/Gondwana.Asteroids.WinForms
dotnet run --project Demos/Gondwana.Asteroids.Avalonia
dotnet run --project Demos/Gondwana.Asteroids.Blazor
```

For browser publishing install the .NET 10 wasm-tools workload. Click the game canvas to focus keyboard input.

- Left/right or A/D: rotate
- Up or W: thrust
- Space: start/fire
- H: safe hyperspace
- P: pause/resume
- R: return to title
- Escape: close desktop window

Inertial flight, toroidal collisions and edge rendering, splitting rocks, enemy saucers, shots, explosion particles, waves, three lives, safe respawning, bonus lives every 10,000 points, and session high score. The game uses circular collision rules rather than axis-aligned sprite collisions, short simulation substeps, and a 1280 x 720 logical world. Art is generated in code; no external assets. This initial version is keyboard-only and silent.
