Dirty rectangles are Gondwana’s redraw currency for the CPU-backed BitmapBackbuffer. They are not used with the GpuBackbuffer.

There are two different kinds , and confusing them causes bugs:
- world dirty rectangles — stored in RefreshQueue
- screen dirty rectangles — tracked by the backbuffer for presentation

---

## World dirty rectangles
These say: “this part of the world needs to be rerendered.”

They are layer-relative in the sense that each layer owns its own queue, but the rectangles themselves are stored in world pixels.

---

## Screen dirty rectangles
These say: “this part of the render surface changed and needs to be presented.”

These live on the backbuffer and are always in adapter/control screen pixels.

---

## Why Gondwana separates them
Because a single world change can project into different screen rectangles depending on:
- camera position
- viewport placement
- zoom
- parallax
- overlapping multi-view layouts

That separation is what makes the view-centric model work cleanly.

---

## Practical takeaway
If you are invalidating scene content, think in world rects.
If you are presenting to the UI adapter, think in screen rects.

---

## Where to read next
- `Gondwana/Rendering/RefreshQueue.cs`
- `Gondwana/Rendering/Views/View.cs`
- `Gondwana/Rendering/Backbuffers/BackbufferBase.cs`
