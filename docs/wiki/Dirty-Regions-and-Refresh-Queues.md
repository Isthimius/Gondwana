# Dirty Regions and Refresh Queues

Gondwana's **CPU-backed bitmap renderer** uses dirty regions to avoid redrawing unchanged scene content. Each `SceneLayer` owns a `RefreshQueue` of **world-space** rectangles describing visual changes. During rendering, each `View` projects those regions into **logical Backbuffer screen pixels**, redraws affected content, and lets the Backbuffer track the changed area for presentation.

This optimization applies to the `BitmapBackbuffer` path. Desktop GL and browser WebGL render complete Scene/View compositions instead; they do **not** consume `SceneLayer.RefreshQueue` or use Backbuffer dirty rectangles to present partial frames.

## Three distinct kinds of dirty region

| Stage | Owner | Coordinate space | Purpose |
| --- | --- | --- | --- |
| Pending world rectangles | `SceneLayer.RefreshQueue` | World pixels | Record *where scene content changed* without depending on a camera |
| Projected per-View screen rectangles | `RenderSurfaceHost.CollectDirtyScreenArea(view)` | Logical Backbuffer `ScreenPx` | Determine patches to clear/redraw for that View |
| Aggregate changed rectangle | `BackbufferBase.DirtyRectangle` | Logical Backbuffer `ScreenPx` | Limit bitmap data sent to the presentation adapter |

**The Backbuffer dirty rectangle is not in native adapter/control pixels.** `PresentationTransform` converts logical `ScreenPx` to destination/adapter pixels when presenting. This distinction matters when `RenderScale`, window size, or aspect ratio differs from the logical Backbuffer size.

The names are similar because each stage deals in invalidation, but they answer different questions:

- **RefreshQueue:** What changed in this layer's world?
- **View projection:** Which logical screen regions does this particular camera see?
- **Backbuffer:** Which part of the completed bitmap needs presentation?

See [[Coordinate Spaces]], [[Backbuffers]], and [[Using Views and Cameras]] for the coordinate and presentation models.

## Where invalidation begins

Visual changes can mark the affected world bounds on their owning layer. Examples include sprite movement, frame or visibility changes, tile modifications, SceneLayer-bound DirectDrawings, and explicit redraw requests.

A moving sprite illustrates why **both its previous and new visual bounds** matter: repainting only the new position can leave its old pixels behind. Invalidating the former region also allows previously obscured tiles, sprites, or drawings to be restored.

World-space storage keeps this independent of Views. Suppose the layer records:

```text
World dirty rectangle: (1000, 500, 64, 64)

SceneLayer.RefreshQueue
    └── world region (1000, 500, 64, 64)
             ├── View A → logical ScreenPx patch A
             ├── View B → logical ScreenPx patch B
             └── View C → not visible; no patch
```

Each View may use a different camera, zoom, parallax, or viewport position. A shared world rectangle must not be interpreted as a single screen rectangle.

Camera changes or changes affecting the overall composition can require a **full refresh** rather than localized invalidation. `Scene.FullRefreshNeeded` causes the bitmap renderer to enqueue each View's visible world extent for each visible layer; it expands those extents by a tile margin to protect projection boundaries. Bitmap view presentation effects and layer wrapping may also require full composition when a scene is dirty.

## How RefreshQueue stores work

The queue is an internal per-`SceneLayer` list of `System.Drawing.Rectangle` values in **world pixels**. Its responsibilities are deliberately narrow:

- `AddWorldRect(rect)` ignores an empty rectangle or an update for which the scene does not use dirty-region rendering.
- Calls made off the engine thread are dispatched onto the engine thread before modifying the queue.
- Mutation and `SnapshotWorldRects()` use a lock; rendering takes point-in-time copies rather than enumerating the mutable list from another thread.
- If an existing rectangle **fully contains** an incoming rectangle, the new rectangle is not added.
- Otherwise the incoming rectangle is appended. **`RefreshQueue` does not remove older contained rectangles or merge overlapping rectangles** when adding a larger/new region.
- `ClearRefreshQueue()` clears the pending list on the engine thread after rendering has processed the visible layers.

The separate `DirtyRectangleExtensions.AddDeduped()` helper is used while collecting **projected screen regions**. It skips regions already contained and merges intersecting rectangles. Do not confuse its behavior with `RefreshQueue.AddWorldRect()`.

`AddViewScreenRect(view, layer, screenRect)` is available internally when an invalidation originates in View screen coordinates: it clips to the View viewport, converts that region back to the layer's world space, adds a small rounding margin, then enqueues the world rectangle.

A queue is **pending redraw work**, not persistent Scene state and not a record of all prior changes. It is reconstructed with its layer rather than serialized.

## Bitmap rendering: world damage to screen damage

At a high level, `RenderSurfaceHost.RenderToBackbufferBitmap(tick)` follows this sequence:

1. Handle a required full refresh, or return early when the scene is clean.
2. For each View, `CollectDirtyScreenArea(view)` snapshots the visible layers' queued world rectangles and projects them with `WorldRectToScreenRect`.
3. Align/clamp the projected rectangles to the View's logical Backbuffer viewport. `AddDeduped` reduces redundant *screen* regions.
4. Clip the drawing canvas to the View viewport and exclude areas covered by blocking higher-Z Views.
5. Pre-clear affected logical screen patches to the Backbuffer clear color.
6. Convert cleared areas into overlapping layers' world coordinates and enqueue them, so background content exposed by removed or moved foreground content is restored.
7. For each visible layer, query drawables intersecting its queued world regions and redraw them within the projected screen clips.
8. Render View-bound overlays, clear consumed layer queues, reset the full-refresh flag, and invoke post-scene hooks.

A screen patch can affect multiple SceneLayers even though its original invalidation came from just one of them. This is why **pre-clearing and re-enqueuing overlapping layers** matter: a clear must not leave a hole where lower-Z artwork should appear.

For the full host lifecycle, including adapter-specific behavior, see [[Bitmap Rendering Path]] and [[Rendering Pipeline]].

## Worked example: moving a sprite

Imagine a sprite occupying world rectangle `(100, 80, 32, 32)` that moves right to `(140, 80, 32, 32)`.

1. The sprite's **old** bounds and **new** bounds are invalidated on its SceneLayer.
2. A View maps each world rectangle to logical screen space through its camera and layer projection. Different Views can produce different screen patches.
3. The bitmap renderer clears those screen patches and ensures all layers contributing visible content to those areas are redrawn.
4. The sprite appears in its new location; the old location now shows its underlying background.
5. The Backbuffer accumulates the affected **logical screen-pixel bounds** for adapter presentation.

The dirty regions specify **where to reconsider drawing**, not a command to paint only the sprite or a promise that exactly two rectangles reach the adapter. Clipping, overlapping content, merging, and full refreshes can expand or combine the work.

## Redrawing versus presenting

Gondwana keeps these optimizations separate:

- **Dirty-region rendering:** `RefreshQueue` determines which scene content the bitmap renderer draws again.
- **Dirty-rectangle presentation:** `BackbufferBase.DirtyRectangle` accumulates changed logical pixels; `RenderSurfaceHost.RedrawDirtyRectangleOnly` determines whether to present that portion or the full completed bitmap.

Setting `RedrawDirtyRectangleOnly = false` presents the entire existing Backbuffer. **It does not disable the layer refresh queues or force the scene itself to redraw completely.**

If post-scene canvas handlers or rendering plugins may touch arbitrary pixels, Gondwana marks the **whole logical Backbuffer dirty** for presentation, even when the normal scene rendering used small dirty regions.

Ordinary adapter resizing adjusts the `PresentationTransform`, not necessarily the logical Backbuffer resolution. A presentation invalidation can cause full image presentation without implying a new scene-wide redraw.

## Why the GPU path differs

`GpuBackbuffer` uses full View composition when a GPU scene frame is produced. The renderer does not consume the bitmap refresh queues and does not maintain a Backbuffer presentation dirty rectangle.

- **Desktop GL:** Engine-thread work records a complete `RenderFrameSnapshot` (Skia drawing commands); the GL callback replays a completed snapshot into the GPU target.
- **Browser WebGL:** A new scene frame is rendered synchronously in the WebGL path when due; an intervening browser paint may re-present the existing GPU image without rebuilding the scene.

This avoids relying on engine-thread queue mutations and clears from a separately owned GL callback. Full-frame drawing and **View clipping** are compatible; clipping limits where a View may draw, whereas dirty regions track which content changed.

## Performance and troubleshooting

- **Sprite trails:** Check whether **old as well as new visual bounds** were invalidated, including effects extending beyond nominal sprite bounds.
- **Missing background after movement or removal:** Check pre-clearing and `EnqueueForOverlappingSceneLayers` for cleared patches.
- **Redraw unexpectedly covers the whole bitmap:** Check `Scene.FullRefreshNeeded`, camera movement, layer wrapping, and View presentation effects.
- **Too many small dirty regions:** Inspect repeated invalidations and projected-screen merging. The world queue performs containment checks but is **not** a general-purpose rectangle union/coalescing structure.
- **Presentation appears stale while scene drawing is correct:** Verify `Backbuffer.DirtyRectangle`, `RedrawDirtyRectangleOnly`, the adapter's presentation transform, and any full-presentation invalidations.
- **Nothing enqueued under GPU rendering:** This is intentional; rendering of GPU scene frames does not depend on the dirty queue.

For diagnostics and broader performance context, see [[Debugging and Instrumentation]] and [[Performance Tuning]]. Correctness takes precedence over minimizing dirty-region size: request a full refresh when the existing bitmap cannot safely be reused.

## Relevant source

- [`Gondwana/Rendering/RefreshQueue.cs`](https://isthimius.github.io/Gondwana/api/latest/RefreshQueue_8cs_source.html)
- [`Gondwana/Rendering/DirtyRectnagleExtensions.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Rendering/DirtyRectnagleExtensions.cs)
- [`Gondwana/Rendering/RenderSurfaceHost.cs`](https://isthimius.github.io/Gondwana/api/latest/RenderSurfaceHost_8cs_source.html)
- [`Gondwana/Rendering/Backbuffers/BackbufferBase.cs`](https://isthimius.github.io/Gondwana/api/latest/BackbufferBase_8cs_source.html)

See also [[Bitmap Rendering Path]], [[Backbuffers]], [[Rendering Pipeline]], and [[Coordinate Spaces]].
