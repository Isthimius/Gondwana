A Backbuffer is Gondwana's **logical off-screen drawing surface**. Scene content is rendered into the Backbuffer first; a platform adapter then presents that result to a WinForms control, Avalonia framebuffer, browser canvas, or another destination.

The Backbuffer is deliberately **not the same thing as the window or canvas**. Its resolution can remain fixed while the destination grows, shrinks, or changes aspect ratio.

Gondwana currently provides two primary implementations:

- `BitmapBackbuffer` — CPU-backed Skia rendering.
- `GpuBackbuffer` — GPU-backed Skia rendering through an active GL/WebGL `GRContext`.

Both inherit from `BackbufferBase`, which lets `RenderSurfaceHost<TBackbuffer>` share the same Scene/View composition model while using different redraw and presentation strategies.

---

## Logical resolution and presentation size

`BackbufferBase.Width` and `BackbufferBase.Height` describe the logical rendering resolution in Gondwana `ScreenPx`.

`RenderSurfaceAdapterBase.Width` and `Height` describe the current platform destination size.

Those values are no longer coupled after the logical resolution has been established.

At surface creation, `Engine.Instance.Configuration.RenderScale` determines the Backbuffer resolution from the first valid adapter size:

```text
logical width  = adapter width  × RenderScale
logical height = adapter height × RenderScale
```

The result is rounded to whole pixels.

Examples:

| Adapter | RenderScale | Logical Backbuffer |
| --- | ---: | --- |
| 1920×1080 | `1.0` | 1920×1080 |
| 3840×2160 | `0.5` | 1920×1080 |
| 1280×720 | `2.0` | 2560×1440 |

Ordinary adapter resize does **not** resize the Backbuffer. Instead, `PresentationTransform.Fit(...)` derives an aspect-preserving scale and centered destination rectangle. `RenderSurfaceHostBase.PresentationScale` exposes that derived scale as read-only state.

Changing `RenderScale` explicitly requests a new logical resolution from the current adapter dimensions.

This separation makes internal-resolution scaling, fixed-resolution games, supersampling, and stable View coordinates possible without tying game rendering to window size.

---

## BackbufferBase responsibilities

`BackbufferBase` defines the rendering contract shared by CPU and GPU implementations. It provides or coordinates:

- the Skia `SKCanvas` used by Gondwana drawables;
- logical Backbuffer dimensions;
- `BeginFrame()` and `EndFrame()` lifecycle hooks;
- tile and drawable rendering support;
- clear color and diagnostic drawing paints;
- snapshot creation through `Snapshot()`;
- explicit logical resize requests;
- bitmap-path dirty-rectangle tracking; and
- disposal of rendering resources.

`RenderSurfaceHost<TBackbuffer>` owns the Backbuffer and selects the appropriate render path through `Backbuffer.IsGlThreadRendered`.

---

## BitmapBackbuffer

`BitmapBackbuffer` is the classic CPU-backed path.

Internally it owns:

- an `SKBitmap` containing CPU-accessible pixels; and
- an `SKSurface` whose canvas draws into that bitmap.

The surface uses BGRA 8888 premultiplied pixels.

### Rendering model

Bitmap rendering normally occurs directly from Gondwana's foreground rendering pass. SceneLayer `RefreshQueue` entries identify changed world regions, and the host redraws only the corresponding Backbuffer areas.

As drawables and cleared regions are rendered, `BackbufferBase.DirtyRectangle` accumulates the logical `ScreenPx` area that changed. That rectangle can then limit how much of the completed bitmap is presented.

### Resizing

`BitmapBackbuffer.RequestResize(width, height)` is deferred and thread-safe. Multiple pending requests are coalesced, and the most recent requested logical resolution is applied during the next `BeginFrame()` on the rendering thread.

Window/control resize alone does not call this path. A logical resize occurs only when Gondwana explicitly requests one, such as after `RenderScale` changes.

### Snapshot semantics

`BitmapBackbuffer.Snapshot()` creates an immutable `SKImage` snapshot that can safely be handed from the rendering context to the platform/UI presentation context.

The platform adapter may display the full image or only the dirty region, depending on `RenderSurfaceHost.RedrawDirtyRectangleOnly`.

See [[Bitmap Rendering Path]].

---

## GpuBackbuffer

`GpuBackbuffer` is Gondwana's GL/WebGL-thread-rendered Backbuffer.

After GPU initialization it owns an off-screen Skia GPU `SKSurface` associated with the active `GRContext`. Tiles, sprites, DirectDrawings, effects, and post-scene canvas work are rasterized directly into that GPU render target.

`GpuBackbuffer.IsGlThreadRendered` is always `true`, which tells the normal engine foreground loop not to render or present that host directly.

### Before the GL/WebGL context exists

A newly constructed `GpuBackbuffer` initially creates a temporary CPU raster surface. This keeps the object in a valid state before a platform GL/WebGL callback can provide a `GRContext`.

When `EnsureInitialized(grContext)` first receives the owning GPU context, the temporary surface is replaced by the real GPU render target.

### Rendering model

GPU scene rendering does not use SceneLayer RefreshQueues or Backbuffer dirty rectangles. Whenever Gondwana requests a new GPU scene frame, `RenderSurfaceHost` clears and redraws the complete View composition.

This avoids the queue synchronization problems that would arise from consuming engine-thread dirty queues from a separate native GL callback. It also fits the GPU presentation model, where Gondwana ultimately presents the complete Backbuffer surface.

On WebGL, a browser animation frame does not necessarily cause a new scene render: the current GPU Backbuffer can be re-blitted when Gondwana's `TargetFPS` cadence has not requested a new scene frame.

### GPU initialization and logical resizing

`GpuBackbuffer.RequestResize(width, height)` queues a logical resolution request. The next owning GL/WebGL callback applies it through `EnsureInitialized(grContext)` and recreates the GPU render target there, where the context is valid.

Adapter resize is deliberately absent from this process. Resizing a desktop window or browser canvas changes only the destination `PresentationTransform` unless `RenderScale` itself changes.

A new `GRContext` also causes `EnsureInitialized` to recreate the GPU surface, which is necessary after context replacement/recreation.

### Snapshot semantics

`GpuBackbuffer.Snapshot()` returns a lightweight GPU-backed `SKImage` view of the current surface. It does **not** read the frame back into CPU memory.

The snapshot must be consumed and disposed while the owning GPU context remains valid. Desktop GL presentation currently uses this snapshot route through `GlRenderAndSnapshot()`.

The WebGL path can often avoid even that scoped image object by drawing the Backbuffer surface directly through `GlRenderToCanvas()` or `GlDrawCurrentFrameToCanvas()`. When linear sampling requires an image, Gondwana uses a scoped GPU texture snapshot rather than a CPU readback.

See [[GL Rendering Path]] and [[WebGL Rendering Path]].

---

## Dirty rectangles are bitmap-only

`BackbufferBase.DirtyRectangle` is always expressed in logical Backbuffer `ScreenPx`.

For bitmap Backbuffers, `ClearRect` and `DrawDrawables` add changed areas to that aggregate rectangle so presentation can be limited to the affected pixels.

For GPU Backbuffers, dirty tracking is intentionally skipped. Calls that would add a dirty area return without doing so when `IsGlThreadRendered` is true.

This distinction is important:

- a View clip limits *where a View may draw*;
- a dirty rectangle limits *what changed and needs bitmap presentation*.

GPU rendering still uses View clipping but does not use dirty-region optimization.

---

## BeginFrame and EndFrame

Both Backbuffers implement the same lifecycle, but the work differs slightly.

### `BeginFrame()`

Both implementations restore the canvas to a known state, reset its matrix, and clip to the logical Backbuffer bounds.

`BitmapBackbuffer.BeginFrame()` also applies a pending logical resize, if one exists.

`GpuBackbuffer` logical resize is applied by `EnsureInitialized()` from the owning GL/WebGL callback before scene rendering.

### `EndFrame()`

Both implementations flush their Skia surface before presentation or snapshot consumption.

For bitmap rendering, `EndFrame()` occurs before the CPU snapshot is sent toward the adapter.

For GPU rendering, it occurs while the active `GRContext` is current and before the GPU Backbuffer is copied/drawn into the platform surface.

---

## PresentationTransform

The Backbuffer itself does not decide where it appears inside a platform destination. That is the adapter's job.

`RenderSurfaceAdapterBase` maintains an immutable `PresentationTransform` based on:

- logical Backbuffer width/height; and
- current adapter width/height.

The transform preserves aspect ratio and centers the image. Unused destination space is cleared, producing letterboxing or pillarboxing when aspect ratios differ.

`RenderScalingFilter` determines whether presentation uses linear or nearest-neighbor sampling.

The same transform is also inverted for pointer input through `AdapterPxToScreenPx`, keeping rendering and input aligned even when the Backbuffer is scaled and centered.

---

## GPU-specific configuration

`GpuBackbuffer` carries several GPU-related settings copied from `EngineConfiguration`:

| Setting | Purpose |
| --- | --- |
| `TargetFps` | Mirrors Gondwana's requested foreground render cadence. The engine/browser loop performs the actual pacing. |
| `VSync` | Used by WinForms GL presentation. Avalonia delegates synchronization to its compositor; browser WebGL follows browser animation/compositor behavior. |
| `MsaaSampleCount` | Requested MSAA sample count for the off-screen GPU surface. Unsupported values fall back to sample count `1`. |

Changing MSAA affects the next GPU surface creation/recreation rather than mutating the existing render target in place.

---

## Thread and context ownership

Backbuffer type does not merely select storage; it also changes who is allowed to render.

| Backbuffer | Normal rendering owner |
| --- | --- |
| `BitmapBackbuffer` | Gondwana's current engine/timer-driven execution context |
| `GpuBackbuffer` on WinForms | WinForms UI/GL `PaintSurface` callback |
| `GpuBackbuffer` on Avalonia | Avalonia OpenGL render callback |
| `GpuBackbuffer` on Blazor | Browser `SKGLView` WebGL paint callback |

After GPU initialization, operations that touch the GPU surface must remain on the callback where its `GRContext` is current.

---

## Choosing a Backbuffer

Use `BitmapBackbuffer` when you need the compatibility path, CPU-accessible rendered pixels, or dirty-region rendering/presentation.

Use `GpuBackbuffer` when the platform provides a supported GL/WebGL render surface and you want the normal GPU path without CPU frame-pixel transfer.

For Blazor, both choices exist: the bitmap host remains available as a Canvas 2D compatibility path, while the GPU host renders through WebGL.

---

## Mental model

```text
Scene + Views
     |
     v
logical Backbuffer (ScreenPx)
     |
     |  PresentationTransform
     v
platform adapter / framebuffer / browser canvas
```

The Backbuffer is the game's working canvas. The adapter is where that canvas is displayed.

Keeping those responsibilities separate is what allows Gondwana to use the same Scene/View rendering model across CPU bitmap, desktop GL, and browser WebGL backends.

---

## Where to read next

- [[Rendering Pipeline]]
- [[Bitmap Rendering Path]]
- [[GL Rendering Path]]
- [[WebGL Rendering Path]]
- [[Refresh Queues]]
- [[Dirty Rectangles]]
- [[Coordinate Spaces]]

Relevant source files:

- [`Gondwana/Rendering/Backbuffers/BackbufferBase.cs`](https://isthimius.github.io/Gondwana/api/latest/BackbufferBase_8cs_source.html)
- [`Gondwana/Rendering/Backbuffers/BitmapBackbuffer.cs`](https://isthimius.github.io/Gondwana/api/latest/BitmapBackbuffer_8cs_source.html)
- [`Gondwana/Rendering/Backbuffers/GpuBackbuffer.cs`](https://isthimius.github.io/Gondwana/api/latest/GpuBackbuffer_8cs_source.html)
- [`Gondwana/Rendering/RenderSurfaceHost.cs`](https://isthimius.github.io/Gondwana/api/latest/RenderSurfaceHost_8cs_source.html)
- [`Gondwana/Rendering/RenderSurfaceAdapterBase.cs`](https://isthimius.github.io/Gondwana/api/latest/RenderSurfaceAdapterBase_8cs_source.html)
- [`Gondwana/Rendering/PresentationTransform.cs`](https://isthimius.github.io/Gondwana/api/latest/PresentationTransform_8cs_source.html)
