# Gondwana Torchlight Dungeon

A compact top-down dungeon walkabout for testing Gondwana's direct-drawing lighting primitives on the **GPU WinForms backbuffer**. The procedurally generated 32px atlas needs no external images or assets.

The player carries a warm, flickering torch. Five independent wall sconces light the passageways. Two switchable darkness overlays (one view-bound, one SceneLayer-bound) each track the same `DirectLightLayer`, automatically revealing terrain as the explorer moves and the camera follows. No custom shaders or lighting substitutes are used by the demo.

## Run

From the repository root on Windows with the .NET SDK:

```powershell
dotnet run --project Demos/Gondwana.Torchlight/Gondwana.Torchlight.csproj
```

When built inside the repository the project references the latest source. Outside the repository it falls back to Gondwana 2.x NuGet packages.

## Controls

| Key | Action |
| --- | --- |
| WASD / Arrow keys | Walk (diagonals normalized; walls block movement) |
| Mouse wheel | Zoom in/out around the followed player (0.5x–3x) |
| X / Z | Zoom in / out |
| C | Reset zoom to 1x |
| V | Toggle the currently selected darkness overlay |
| B | Switch between view-bound and SceneLayer-bound darkness |
| F | Toggle flicker on all torches |
| T | Toggle the player's torch (glow *and* reveal) |
| L | Toggle the five wall sconces (glows *and* reveals) |
| + / - (including numpad) | Adjust carried light radius, 60–270 px |
| R | Respawn at the entry room |
| Escape | Exit |

The **window can be resized or maximized**; the GPU render surface follows the window size. Gondwana preserves the logical backbuffer resolution and fits it without distortion, so different window aspect ratios may show letterboxing. Zooming changes the actual world view independently of window sizing.

The HUD displays **GPU FPS**, measured from the current GPU surface’s `presentation.count` telemetry and refreshed approximately four times a second. The FPS value shows `--` until the first sample arrives.

## What to inspect

- `DirectLightLayer`: each light is a world-pixel scene-layer `DirectRadialLight`, not a regular `SceneLayer`.
- `DirectDarknessOverlay` vs `DirectSceneLayerDarknessOverlay`: press **B** to compare view-space darkness against world-bounded SceneLayer darkness; each tracks all lights via `TrackLightLayer`.
- Flicker: all six lights have independent frequencies; tracked reveal intensity changes together with the light.
- Movement: sprite positions are in grid coordinates, so `TileCenter` converts to world pixels for the light; both stay aligned as the camera pans.
- Zoom: the Viewport changes magnification while the Camera remains centered on the player; the light and darkness overlays continue using world positions.
- Resizing: window/client size affects presentation scaling, not the logical backbuffer or camera zoom.
- Performance: the FPS readout is the actual GPU presentation rate, which may differ from the engine simulation rate.
- Layering: floor tiles, walls/sprites, light glows, one selected darkness overlay, and the HUD use distinct layers / Z-order ranges.

**Limitations:** This intentionally tests soft radial glows and reveals, not ray-cast visibility or shadows. Light passes visually through walls because the underlying primitives do not implement occlusion. Collision is a small demo-local tile grid check.

See the [Direct Lighting and Darkness wiki](https://github.com/Isthimius/Gondwana/wiki/Direct-Lighting-and-Darkness) for the engine API and design.
