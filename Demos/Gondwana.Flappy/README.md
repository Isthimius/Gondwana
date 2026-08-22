# Gondwana Flappy

A small Flappy Bird-style demo built entirely with Gondwana and generated art.

The code for this project was generated with an LLM-assisted workflow in under 4 minutes using a single prompt:

~~~text
can you create a Flappy Bird clone using Gondwana?
~~~
## Controls

- **Space** — start / flap / retry after game over
- **R** — reset the run
- **Esc** — quit

## What the demo exercises

- `WinFormsGameHost` and the bitmap render surface
- procedural `Tilesheet` creation with SkiaSharp
- `SceneLayer` backgrounds and tile frames
- sprites with scaled render sizes
- integrated sprite acceleration and velocity for the bird
- sprite collision areas for pipes and the bird
- `TextBlock` HUD / game-state messaging
- fixed-step-friendly game logic driven by the engine cycle hooks

The pipe art, bird, clouds, and ground are generated in code. No external Flappy Bird assets are included.

## Run it

From the repository root:

```powershell
dotnet run --project .\Demos\Gondwana.Flappy\Gondwana.Flappy.csproj
```

If you want the project visible in `Gondwana.sln`, add it with:

```powershell
dotnet sln Gondwana.sln add .\Demos\Gondwana.Flappy\Gondwana.Flappy.csproj --solution-folder Demos
```
