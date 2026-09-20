Parallax in Gondwana is not a special effect bolted onto the renderer. It is a first-class property of `SceneLayer`.

Each layer has a Parallax factor:
- &lt; 1 moves slower than the camera
- = 1 moves normally
- &gt; 1 moves faster than the camera

Because view transforms apply parallax during world-to-screen conversion, the effect naturally falls out of the normal render path.

---

## Multi-view rendering
A RenderSurfaceHost can own multiple views. Each one can have:
- its own camera
- its own viewport rectangle
- its own zoom
- its own z-order

This supports:
- split-screen
- minimaps
- picture-in-picture
- layered overlay views

---

## Overlap behavior
Views are rendered in ascending ZOrder. Higher views can clip lower ones where their viewport rectangles overlap.

That gives Gondwana deterministic compositing even when views share screen space.

---

## Key insight
Parallax is layer-relative.
Multi-view is render-surface-relative.

Those are different axes of composition, and Gondwana supports both at once.

---

## Where to read next
- [`Gondwana/Scenes/SceneLayer.cs`](https://isthimius.github.io/Gondwana/api/latest/SceneLayer_8cs_source.html)
- [`Gondwana/Rendering/Views/View.cs`](https://isthimius.github.io/Gondwana/api/latest/View_8cs_source.html)
- [`Gondwana/Rendering/Views/ViewManager.cs`](https://isthimius.github.io/Gondwana/api/latest/ViewManager_8cs_source.html)
- [`Gondwana/Rendering/RenderSurfaceHost.cs`](https://isthimius.github.io/Gondwana/api/latest/RenderSurfaceHost_8cs_source.html)
