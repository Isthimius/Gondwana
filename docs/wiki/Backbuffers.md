A backbuffer is the in-memory drawing surface Gondwana renders into before anything is presented to the screen.

The engine supports two main backbuffer types:
- BitmapBackbuffer
- GpuBackbuffer

Both inherit from BackbufferBase, so the higher-level render pipeline can stay mostly the same.

---

## BitmapBackbuffer
This is the classic CPU-rendered path.

It uses an in-memory SKBitmap + SKSurface, supports deferred resize requests, and is designed around dirty-rectangle rendering and safe snapshotting for UI presentation.

This is the backbuffer that benefits from Gondwana’s dirty-region system.

---

## GpuBackbuffer
This is the GL-thread-rendered path.

Instead of rendering on the engine’s normal foreground loop and then copying to the adapter, GPU surfaces render directly on the GL thread into a GPU render target. The engine treats these differently because partial dirty-rectangle presentation is not the main optimization there.

---

## Shared responsibilities
A backbuffer is responsible for:
- exposing an SKCanvas
- beginning and ending a frame
- drawing tiles and drawables
- tracking dirty screen pixels
- snapshotting rendered output
- resizing when needed

---

## Mental model
The backbuffer is not the window. It is the engine’s working canvas.

---

## Where to read next
- `Gondwana/Rendering/Backbuffers/BackbufferBase.cs`
- `Gondwana/Rendering/Backbuffers/BitmapBackbuffer.cs`
- `Gondwana/Rendering/Backbuffers/GpuBackbuffer.cs`
