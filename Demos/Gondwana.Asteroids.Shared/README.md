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
- M: mute/unmute
- R: return to title
- Escape: close desktop window

Inertial flight, toroidal collisions and edge rendering, splitting rocks, enemy saucers, shots, explosion particles, waves, three lives, safe respawning, bonus lives every 10,000 points, and session high score. The game uses circular collision rules rather than axis-aligned sprite collisions, short simulation substeps, and a 1280 x 720 logical world. Sprite art is generated in code. The HUD and menus use an embedded Architects Daughter font (SIL Open Font License, see [Fonts/OFL.txt](Fonts/OFL.txt)) rather than platform system fonts, so browser and desktop ports render the same text without fetching a separate asset. The game is keyboard-only.

## Audio

Original synthesized PCM WAV effects are generated once at load time in the shared project: player and saucer fire, size-dependent rock explosions, ship and saucer explosions, hyperspace, bonus life, looping thrust and saucer tones, and an alternating beat that accelerates as the field clears. M toggles sound; pausing, resetting, and disposal stop active audio. No original arcade recordings or external audio assets are used.

WinForms uses the built-in NAudio backend, Avalonia uses NAudio on Windows, and Blazor uses the browser backend (click the canvas first to permit playback). Avalonia on macOS and Linux remains playable without audio until a compatible backend is available.
