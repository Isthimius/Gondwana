A `RefreshQueue` is Gondwana’s dirty-region tracking structure for CPU-backed rendering.

Each `SceneLayer` owns one. The queue records areas of the layer that have changed visually and therefore need to be redrawn.

The important detail is that those regions are stored in **world pixel space**.

They are not screen-space rectangles, viewport rectangles, or adapter coordinates.

That distinction is deliberate.

---

## Why refresh queues exist

Redrawing an entire scene every frame is simple, but it can be unnecessarily expensive for CPU-backed rendering.

If only a small portion of a layer changed, Gondwana can instead redraw only the affected portion.

For example, if a sprite moves from one location to another, the engine does not necessarily need to redraw the entire backbuffer.

It primarily needs to redraw the regions containing:

- the sprite's old position
- the sprite's new position

Those changed world regions are added to the layer's `RefreshQueue`.

The queue therefore acts as an accumulation point for visual invalidation between rendered frames.

---

## What goes into a RefreshQueue

Whenever something changes visually in a `SceneLayer`, the engine can enqueue a world-space rectangle describing the affected area.

Typical causes include:

- sprite movement
- sprite visibility changes
- tile changes
- direct-drawing changes
- layer-origin changes
- explicit refresh requests
- camera or view changes that require scene content to be reconsidered

A refresh rectangle does not describe what should be drawn.

It only says:

> Something inside this area may no longer match the current rendered image.

The renderer determines what scene content intersects that area and needs to be drawn again.

---

## Why the queue uses world space

A `SceneLayer` belongs to the scene, not to any one `View`.

That matters because the same layer may be rendered through multiple views.

Suppose a sprite changes at this world location:

```text
World rectangle
(1000, 500, 64, 64)
```

One view may currently show that region near the center of its viewport.

Another view may show it near an edge.

A third view may not show it at all.

If the `RefreshQueue` stored screen-space rectangles, the layer would need separate invalidation information for every possible view.

Instead, Gondwana records the change once:

```text
SceneLayer
    |
    v
RefreshQueue
    |
    | world-space dirty rectangle
    v
(1000, 500, 64, 64)
```

Each `View` then decides how that world-space region maps onto its own screen area.

---

## From world dirty region to screen dirty region

During CPU rendering, the renderer examines the queued world rectangles for each layer.

For each relevant `View`, those regions are projected through the same transformations used for normal rendering:

```text
World dirty rectangle
        |
        | camera
        | parallax
        | zoom
        | viewport placement
        v
Screen dirty rectangle
```

The resulting screen-space regions determine which portions of the backbuffer need to be updated.

This is an important separation of responsibilities:

- `RefreshQueue` knows **what changed in the world**
- `View` knows **where that world region appears on screen**
- the backbuffer knows **which screen pixels must be redrawn**

---

## One change, multiple views

The world-space design becomes especially useful when a scene has more than one view.

Conceptually:

```text
                   +-> View A -> Screen rect A
World dirty rect --|
                   +-> View B -> Screen rect B
```

There is still only one underlying change to the scene.

Each view independently projects it according to its:

- camera
- viewport
- zoom
- parallax behavior

A view that cannot see the dirty world region does not need to redraw it.

This keeps scene invalidation independent from presentation.

---

## Queue behavior

`RefreshQueue` does more than simply append rectangles indefinitely.

### Redundant regions are avoided

If a new dirty rectangle is already completely covered by an existing one, storing both would provide no additional information.

Containment checks help prevent unnecessary queue growth.

Likewise, a larger invalidation region can supersede smaller contained regions.

The goal is not to maintain a historical record of every visual change.

The goal is to describe the world regions that need attention on the next render.

---

### Changes are marshaled to the engine thread

Visual changes may originate from code that is not currently running on Gondwana's engine thread.

Refresh-queue mutation is therefore routed back through the engine's normal threading model when necessary.

This keeps queue state synchronized with rendering rather than requiring every caller to coordinate access directly.

---

### Rendering works from a snapshot

Rendering consumes a stable snapshot of the dirty regions rather than depending on the queue remaining unchanged throughout the entire render operation.

Conceptually:

```text
Visual changes
      |
      v
RefreshQueue
      |
      | snapshot
      v
Renderer
```

New invalidations that occur while rendering can therefore belong to subsequent work rather than unpredictably changing the set currently being processed.

---

### Queues are cleared after rendering

Once the current dirty regions have been processed, they no longer need to remain in the queue.

The rendered backbuffer now reflects those changes.

Future changes will enqueue new invalidation regions.

So the normal lifetime is:

```text
Change occurs
    |
    v
Region queued
    |
    v
Region rendered
    |
    v
Queue cleared
```

The queue represents **pending invalidation**, not persistent scene state.

---

## Full refreshes

Not every visual change is worth tracking as a collection of small rectangles.

Some operations effectively invalidate everything visible.

In those cases, Gondwana can request a full refresh rather than trying to preserve fine-grained dirty-region information.

This is appropriate when the renderer can no longer safely assume that the existing backbuffer remains valid over most of the surface.

Dirty rectangles are an optimization.

Correct rendering takes priority over preserving them.

---

## CPU rendering only

`RefreshQueue` is used by Gondwana's CPU-backed `BitmapBackbuffer` rendering path.

The GPU rendering path does **not** rely on refresh queues.

GPU-backed rendering currently performs a full render for each GL paint callback rather than attempting to maintain CPU-style dirty-region tracking.

So the high-level distinction is:

```text
BitmapBackbuffer
    |
    +-> RefreshQueue / dirty-region rendering

GpuBackbuffer
    |
    +-> full-frame rendering
```

This is intentional.

The refresh-queue mechanism is designed around Gondwana's engine-thread-driven bitmap rendering model. GL rendering occurs under a different threading and presentation model, where attempting to share the same queue semantics would introduce synchronization problems and little practical benefit.

---

## RefreshQueue vs screen dirty rectangles

These two concepts are related, but they are not the same thing.

A `RefreshQueue` contains:

```text
World-space invalidation
```

The renderer derives:

```text
Screen-space invalidation
```

from it.

That difference is easy to miss because both are commonly described as "dirty rectangles."

A useful way to distinguish them is:

```text
RefreshQueue
    = what changed in the scene

Screen dirty region
    = what pixels need to change on this render surface
```

The first belongs to the scene layer.

The second belongs to rendering and presentation.

---

## Mental model

Think of a `RefreshQueue` as a layer's **pending visual damage list**.

Something changes in the world:

```text
World changed
     |
     v
RefreshQueue
```

Each view asks:

```text
Where does that changed area appear for me?
```

The renderer then updates only the affected screen regions.

Or, more simply:

```text
RefreshQueue -> What changed?
View         -> Where is it visible?
Backbuffer   -> What pixels must be redrawn?
```

That separation is what allows Gondwana's CPU renderer to use dirty-region rendering without tying scene state to a particular camera, viewport, or render surface.

---

## Where to read next

- `Gondwana/Rendering/RefreshQueue.cs`
- `Gondwana/Rendering/RenderSurfaceHost.cs`
- `Gondwana/Rendering/Backbuffers/BitmapBackbuffer.cs`
- [[Dirty Rectangles]]
- [[Bitmap Rendering Path]]
- [[GL Rendering Path]]