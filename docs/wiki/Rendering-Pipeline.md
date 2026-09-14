Gondwana’s render path is intentionally explicit. Bitmap backbuffers use dirty-region redraw to avoid unnecessary CPU-side pixel work. GPU backbuffers, by contrast, render through a GPU-backed `SKSurface`, where the engine can favor full-frame redraw and let the GPU/Skia pipeline handle the heavier lifting.

---

## High-level flow
Each engine cycle does two broad things:

### Background work
- pre-cycle timers
- input polling
- animation advancement
- sprite movement
- collision resolution
- camera updates

### Foreground work
- direct drawing updates
- render visible views into backbuffers
- present backbuffers to adapters
- post-cycle timers

---

## Render path on bitmap backbuffers
1. If the scene needs a full refresh, visible world regions are enqueued for each layer
2. Each view collects dirty world rectangles and converts them to screen-space rectangles
3. Dirty screen areas are pre-cleared
4. Each visible layer redraws only drawables intersecting those dirty world regions
5. View-level direct drawings are rendered on top
6. Backbuffer dirty screen area is presented to the platform adapter

---

## Render path on GPU backbuffers
GPU surfaces currently take a different path: they redraw the full viewport each GL paint. This avoids cross-thread refresh-queue races and fits the GL-thread ownership model better.

---

## Why this design works
The engine separates:
- what changed (RefreshQueue)
- what is visible (View)
- what gets drawn (Backbuffer)
- how it reaches the screen (RenderSurfaceAdapterBase)

That keeps the core engine predictable and platform-agnostic.

---

## Where to read next
- `Gondwana/Engine.cs`
- `Gondwana/Rendering/RenderSurfaceHost.cs`
- `Gondwana/Rendering/Backbuffers/`
