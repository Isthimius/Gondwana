Gondwana is designed to be inspectable while it runs. The engine exposes lifecycle events, render-surface events, runtime performance samples, logging infrastructure, and visual overlays that can help narrow a problem to a specific stage of the engine.

The most useful debugging question is usually not simply *“Why is this wrong?”* It is:

> At which stage did the engine stop doing what I expected?

A visible result normally passes through several stages:

1. game state changes during an engine cycle
2. movement, animation, input, or collision work updates the affected object
3. the scene determines what needs to be redrawn
4. the active view projects world content into screen space
5. the backbuffer draws the content
6. the render-surface adapter presents the result

Instrumentation is most valuable when it identifies which of those stages still behaved correctly.

---

## A quick debugging setup

The following is a reasonable temporary starting point while diagnosing a game:

```csharp
using Gondwana;
using Gondwana.Logging;
using Microsoft.Extensions.Logging;

EngineLogger.SetLogLevel(LogLevel.Debug);

Engine.Instance.CPSCalculated += sample =>
{
    string gpuFps = sample.GpuFps is double value
        ? value.ToString("F1")
        : "n/a";

    Engine.Logger.LogInformation(
        "CPS {GrossCps:F1}; foreground FPS {NetFps:F1}; GPU FPS {GpuFps}",
        sample.GrossCPS,
        sample.NetCPS,
        gpuFps);
};

worldLayer.ShowGridLines = true;
worldLayer.ShowCollisionBoxes = true;
```

This provides three different kinds of evidence:

- logs describing what the game believes it is doing
- periodic measurements of engine and rendering activity
- visual confirmation of grid and collision geometry

Do not leave high-volume logging or debug overlays enabled merely out of habit. Instrumentation consumes some of the time it is attempting to measure.

---

## Start by identifying the rendering path

Bitmap and GPU backbuffers do not use the same redraw strategy. Determine which one is active before investigating invalidation or presentation behavior.

| Path | Rendering behavior | Dirty-region behavior |
| --- | --- | --- |
| Bitmap backbuffer | Renders on the engine thread during the foreground portion of an engine cycle | Consumes per-layer world-space refresh queues and can present only the resulting screen-space dirty area |
| GPU backbuffer | Renders on the GL thread when the platform raises its paint callback | Redraws the complete viewport and deliberately bypasses refresh queues |

This distinction matters. A missing refresh rectangle can explain a bitmap-only artifact, but it cannot explain a GPU-only artifact because the GPU path does not consume `RefreshQueue` at all.

For a GPU-only problem, concentrate on drawable selection, coordinate projection, GL-thread behavior, canvas state, and post-scene drawing. For a bitmap-only problem, invalidation and dirty-region projection belong much higher on the suspect list.

---

## Runtime sampling: CPS, foreground FPS, and GPU FPS

Gondwana periodically raises `Engine.CPSCalculated` with a `CyclesPerSecondCalculatedEventArgs` sample. The default sampling interval is 1.5 seconds and is controlled by `EngineConfiguration.SamplingTimeForCPS`.

Setting `SamplingTimeForCPS` to zero disables sampling:

```csharp
Engine.Instance.Configuration.SamplingTimeForCPS = 1.0;
```

The sample contains three rates with different meanings.

### `GrossCPS`

`GrossCPS` is the number of complete engine cycles per second. It includes cycles that perform background work without rendering a foreground frame.

Background work currently includes:

- input polling
- timer events
- animation advancement
- sprite movement
- collision resolution
- camera updates

A high gross CPS therefore does not prove that frames are reaching the display smoothly.

### `NetCPS`

`NetCPS` counts engine cycles that entered the foreground portion of the loop. It is also exposed through `Engine.FramesPerSecond` after the latest sample.

For a bitmap surface, this is a useful approximation of the engine-driven frame rate. For a GPU surface, it is not the actual number of GL paint callbacks because GPU drawing is driven independently by the platform adapter.

### `GpuFps`

`GpuFps` counts actual frames rendered by registered GPU backbuffers. It is `null` when no GPU surface is registered.

With one GPU surface, it represents that surface's observed paint rate. With multiple GPU surfaces, Gondwana currently reports their combined frame count over the sampling window rather than a per-surface rate.

### Displaying the built-in sample

`CyclesPerSecondCalculatedEventArgs.ToString()` already produces a readable multi-line summary:

```csharp
Engine.Instance.CPSCalculated += sample =>
{
    debugTextBlock.SetText(sample.ToString());
};
```

`CPSCalculated` is posted through the engine's UI dispatcher. It is suitable for updating ordinary UI or debug text, but handlers should still remain lightweight.

The Spot demo logs this sample, while the Slider demo displays the individual values in its UI. Both are useful reference implementations.

---

## Logging

Gondwana routes engine and game logs through `EngineLogger` and the standard `Microsoft.Extensions.Logging` abstractions.

```csharp
using Microsoft.Extensions.Logging;

_gameHost.Initialize(logLevel: LogLevel.Debug);
```

Use typed loggers and structured message templates for permanent game diagnostics:

```csharp
private static ILogger<MyGameHost> Log =>
    EngineLogger.GetLogger<MyGameHost>();

Log.LogDebug(
    "Player {PlayerName} entered level {LevelName}",
    playerName,
    levelName);
```

Asynchronous logging is the normal runtime mode. It protects the engine thread, but messages can arrive slightly later and the bounded queue drops new records if it becomes saturated. Temporarily switch to synchronous logging when exact ordering matters, then restore asynchronous mode before measuring ordinary performance.

See [Logging](https://github.com/Isthimius/Gondwana/wiki/Logging) for typed logger setup, structured messages, levels, exceptions, event IDs, asynchronous and synchronous modes, runtime level changes, platform behavior, shutdown flushing, and troubleshooting.

---

## Visual debugging

Every `SceneLayer` exposes two development overlays:

```csharp
worldLayer.ShowGridLines = true;
worldLayer.ShowCollisionBoxes = true;
```

Changing either property automatically marks the scene for a full refresh so the overlay is added or removed on the next render.

### Grid lines

`ShowGridLines` draws the projected boundaries of visible, fixed-position layer tiles. It is useful for diagnosing:

- incorrect tile dimensions
- layer origins
- projection behavior
- unexpected gaps or overlap
- screen-to-grid picking

Because grid lines follow the active coordinate system, they are particularly valuable for isometric, oblique, and hexagonal layers where a rectangular mental model can be deceptive.

### Collision boxes

`ShowCollisionBoxes` draws the effective collision rectangle for visible tiles and sprites whose `CollisionsEnabled` property is `true`.

If no box appears, first verify:

- the tile or sprite is visible
- `CollisionsEnabled` is `true`
- the expected frame is current
- the object belongs to the layer on which the overlay was enabled

The displayed rectangle includes the effective collision adjustment associated with the current frame or tile. It therefore provides a direct way to verify `.gts` region/frame defaults and any tile-level override.

The overlay shows geometry. It does not show collision groups, masks, profiles, blocking-versus-trigger semantics, or the result of collision resolution. A correctly drawn box proves where the engine believes the collider is; it does not prove that two colliders are configured to interact.

---

## Engine lifecycle hooks

Engine events are useful for broad instrumentation and for determining whether game work occurs before or after a particular engine subsystem.

| Event | Useful diagnostic purpose |
| --- | --- |
| `PreInitialization` | Observe setup before configuration and engine subsystems are initialized |
| `PostInitialization` | Inspect the engine after internal initialization |
| `InitializationComplete` | Begin diagnostics that require a fully initialized engine |
| `BeforeBackgroundTasksExecute` | Start timing input, animation, movement, collision, and camera work |
| `AfterBackgroundTasksExecute` | Stop background timing or inspect the resulting state |
| `BeforeFrameRender` | Inspect state immediately before the engine foreground phase |
| `AfterFrameRender` | Observe completion of the engine foreground phase |
| `CPSCalculated` | Consume periodic cycle and rendering-rate samples |
| `Disposing` | Inspect still-readable state before managed teardown |
| `Disposed` | Confirm teardown has completed |

See [Gondwana Engine Lifecycle](https://github.com/Isthimius/Gondwana/wiki/Gondwana-Engine-Lifecycle) for the complete ordering of these events.

### Timing background work

The before/after events can provide a lightweight aggregate measurement:

```csharp
using System.Diagnostics;

var backgroundTimer = new Stopwatch();
long measuredCycles = 0;
double totalMilliseconds = 0;

Engine.Instance.BeforeBackgroundTasksExecute += () =>
{
    backgroundTimer.Restart();
};

Engine.Instance.AfterBackgroundTasksExecute += () =>
{
    backgroundTimer.Stop();
    totalMilliseconds += backgroundTimer.Elapsed.TotalMilliseconds;
    measuredCycles++;

    if (measuredCycles < 120)
        return;

    Engine.Logger.LogDebug(
        "Average background work: {AverageMs:F3} ms",
        totalMilliseconds / measuredCycles);

    measuredCycles = 0;
    totalMilliseconds = 0;
};
```

This measures the region between the two event invocations, including the behavior of other handlers attached within that boundary. It is good directional evidence, not a substitute for a profiler.

### GPU timing caveat

`BeforeFrameRender` and `AfterFrameRender` bracket the engine's foreground phase. Bitmap backbuffers render inside that phase, but GPU backbuffers render later on the GL thread in response to the adapter's paint callback.

Consequently, the engine frame events do not measure actual GPU drawing time. Use render-surface events or a GPU profiler when the GL render itself is the subject of the investigation.

---

## Render-surface instrumentation

Concrete `RenderSurfaceHost<TBackbuffer>` instances expose events around the actual backbuffer-rendering operation.

| Event | Meaning |
| --- | --- |
| `RenderBackbufferBegin` | A backbuffer render operation has started |
| `RenderBackbufferEnd` | That render operation has completed |
| `RenderBackbufferNoOp` | Bitmap rendering was skipped because the scene had no pending dirty work |
| `RenderBackbufferPostScene` | Scene content has been drawn, but the backbuffer has not yet been finalized and presented |

In a WinForms game host, the concrete host is available through `RenderSurface.Host`:

```csharp
long renderedFrames = 0;
long skippedBitmapFrames = 0;

RenderSurface.Host.RenderBackbufferEnd += () =>
{
    Interlocked.Increment(ref renderedFrames);
};

RenderSurface.Host.RenderBackbufferNoOp += () =>
{
    Interlocked.Increment(ref skippedBitmapFrames);
};
```

Count or aggregate hot events instead of logging every invocation. `RenderBackbufferNoOp` is specific to the bitmap dirty-region path; a GPU backbuffer performs a full render and does not raise it merely because the scene is unchanged.

### Threading

Render-surface hooks run on the thread performing the render:

- bitmap hooks run on the engine thread
- GPU hooks run on the GL thread

A GPU surface creates its host when the control and GL adapter are initialized. Subscribe only after that initialization has occurred.

`RenderBackbufferPostScene` receives the active `SKCanvas`. On a GPU surface, use it directly on the GL thread while the graphics context is current. Do not marshal GPU canvas operations to another thread.

If a handler changes the canvas matrix or clipping region, save and restore the canvas state:

```csharp
RenderSurface.Host.RenderBackbufferPostScene += canvas =>
{
    canvas.Save();

    try
    {
        // Draw temporary screen-space annotations here.
    }
    finally
    {
        canvas.Restore();
    }
};
```

This event is not raised when bitmap rendering is skipped or when the host has no configured views.

---

## Diagnosing bitmap invalidation and presentation

The bitmap path has two related but distinct optimizations:

1. redraw only the scene regions that changed
2. present only the union of changed screen regions to the platform adapter

Those stages are easy to conflate. Gondwana provides separate controls that help isolate them.

### Force an actual scene redraw

Set `Scene.FullRefreshNeeded` to request a full redraw on the next bitmap render:

```csharp
scene.FullRefreshNeeded = true;
```

If a visual artifact disappears, the backbuffer and adapter can probably draw the expected result. The likely defect is earlier in the invalidation path: the changed object did not enqueue the correct world-space area, or that area did not project to the expected screen region.

To force a refresh continuously for a short comparison:

```csharp
void ForceFullRefresh() => scene.FullRefreshNeeded = true;

Engine.Instance.BeforeFrameRender += ForceFullRefresh;

// Run the diagnostic, then remove it.

Engine.Instance.BeforeFrameRender -= ForceFullRefresh;
```

Do not mistake this for a fix. It deliberately discards the performance benefit of bitmap dirty-region rendering.

GPU backbuffers already redraw the full viewport, so this test is primarily meaningful for bitmap surfaces.

### Present the complete existing backbuffer

Set `RedrawDirtyRectangleOnly` to `false` to present the complete bitmap backbuffer to the platform adapter:

```csharp
RenderSurface.Host.RedrawDirtyRectangleOnly = false;
```

Despite the historical property name, this changes presentation behavior. It does **not** force the scene to be rendered from scratch and does not bypass the per-layer refresh queues.

Interpret the result accordingly:

- If `Scene.FullRefreshNeeded = true` fixes the artifact, suspect scene invalidation or dirty-region projection.
- If full-buffer presentation fixes the artifact without requiring a full scene redraw, suspect screen-space dirty bounds or partial presentation in the adapter.
- If neither changes the artifact, inspect state, drawable selection, projection, z-order, clipping, and the drawing operation itself.

Restore `RedrawDirtyRectangleOnly` after the test:

```csharp
RenderSurface.Host.RedrawDirtyRectangleOnly = true;
```

### Source-level refresh-queue inspection

`SceneLayer.RefreshQueue`, `Scene.IsDirty`, and `BackbufferBase.DirtyRectangle` are engine implementation details rather than public game APIs. They are still valuable when stepping through Gondwana itself:

- refresh-queue rectangles are always stored in world pixels
- they are projected into screen space separately for each view
- the backbuffer dirty rectangle is always in adapter/control screen pixels
- the GPU path rejects and clears refresh-queue work because it does not consume dirty regions

When debugging ordinary game code, prefer the public forcing tests and render events above. Inspect the internal queue only when the evidence points to an engine-level invalidation defect.

---

## Coordinate diagnostics

Rendering, picking, and collision bugs often turn out to be coordinate-space bugs. Keep these spaces distinct:

- grid coordinates
- world pixels
- screen pixels
- layer-local projection space

A useful picking diagnostic records a complete round trip:

```csharp
var view = RenderSurface.Host.ViewManager.Views[0];
var screenPosition = mouseArgs.CurrentPosition;

var worldPosition = view.ScreenPxToWorldPx(worldLayer, screenPosition);
var gridPosition = view.ScreenPxToGrid(worldLayer, screenPosition);
var gridWorldPosition = worldLayer.GridToWorldPx(gridPosition);
var gridScreenPosition = view.WorldPxToScreenPx(
    worldLayer,
    gridWorldPosition);

Engine.Logger.LogTrace(
    "screen={Screen}; world={World}; grid={Grid}; grid-origin-screen={GridScreen}",
    screenPosition,
    worldPosition,
    gridPosition,
    gridScreenPosition);
```

The original screen position and the screen position of the selected grid cell are not expected to be identical unless the pointer is exactly on the cell origin. They should, however, describe the same visible cell and respond consistently to camera movement, zoom, parallax, and layer origin changes.

When the round trip is wrong, inspect the first conversion that becomes unexpected rather than compensating for the final error with an arbitrary offset.

See [Coordinate Spaces](https://github.com/Isthimius/Gondwana/wiki/Coordinate-Spaces) and [Views and Cameras](https://github.com/Isthimius/Gondwana/wiki/Views-and-Cameras) for the complete coordinate model.

---

## Collision diagnostics

Collision resolution occurs during the engine's background work after sprite movement. That means `AfterBackgroundTasksExecute` observes the state after movement and collision resolution for the current cycle.

When a collision behaves unexpectedly:

1. enable `ShowCollisionBoxes` on the layer
2. verify `Visible` and `CollisionsEnabled`
3. confirm that the current frame supplies the expected effective collision adjustment
4. verify the collider's collision type
5. verify group/profile and mask configuration
6. confirm that the two objects are intended to interact
7. if an object crosses through another at high speed, distinguish incorrect geometry from movement that traversed the collision area between checks

The overlay answers *where the collider is*. Logs or breakpoints around the collision resolver answer *whether the candidate pair was considered* and *what response was applied*.

Do not enlarge a collision box merely to conceal a tunnelling or resolution problem. That usually trades an obvious defect for collisions that feel mysteriously early.

---

## Common symptoms

| Symptom | First things to check |
| --- | --- |
| No `CPSCalculated` events arrive | Confirm the engine is running and `SamplingTimeForCPS` is greater than zero |
| Gross CPS is high but animation looks slow | Compare `NetCPS` and, for GPU rendering, `GpuFps` |
| GPU motion stutters while `NetCPS` looks healthy | Treat `GpuFps` as the relevant render rate and inspect the GL paint path |
| Logs appear late or out of order | Check whether asynchronous logging is active; temporarily use synchronous mode when ordering matters |
| Logs disappear under extreme volume | Check the enabled level and remember that a saturated asynchronous queue drops new records |
| Collision boxes do not appear | Verify layer selection, object visibility, and `CollisionsEnabled` |
| Collision boxes look correct but no collision occurs | Inspect collision type, profiles, groups, masks, and resolver behavior |
| A bitmap-rendered object changes state but leaves stale pixels | Force `Scene.FullRefreshNeeded`; if that fixes it, investigate invalidation |
| Full-buffer presentation fixes a bitmap artifact | Inspect screen-space dirty bounds and adapter partial presentation |
| GPU rendering works but bitmap rendering is stale | Investigate refresh queues and world-to-screen dirty-region projection |
| Bitmap rendering works but GPU rendering is wrong | Investigate GL-thread drawing, canvas state, projection, or GPU-specific adapter behavior |
| `RenderBackbufferNoOp` never fires on a GPU surface | Expected: the GPU path performs full rendering rather than dirty-scene no-ops |
| Mouse picking is consistently offset | Log screen → world → grid → world → screen conversions and inspect camera, zoom, parallax, and layer origin |

---

## Keep instrumentation from becoming the problem

Before considering an investigation complete:

- disable grid and collision overlays that are no longer needed
- restore dirty-rectangle-only presentation if it was disabled
- remove any forced full-refresh handler
- remove or aggregate per-cycle logging
- restore asynchronous logging if synchronous mode was only diagnostic
- unsubscribe temporary event handlers
- measure again without the heavy diagnostics enabled

Instrumentation should make the engine easier to understand without quietly becoming part of the workload being measured.

---

## Where to read next

- [Logging](https://github.com/Isthimius/Gondwana/wiki/Logging)
- [Gondwana Engine Lifecycle](https://github.com/Isthimius/Gondwana/wiki/Gondwana-Engine-Lifecycle)
- [Performance Tuning](https://github.com/Isthimius/Gondwana/wiki/Performance-Tuning)
- [Refresh Queues](https://github.com/Isthimius/Gondwana/wiki/Refresh-Queues)
- [Bitmap Path](https://github.com/Isthimius/Gondwana/wiki/Bitmap-Path)
- [GL Path](https://github.com/Isthimius/Gondwana/wiki/GL-Path)
- [Coordinate Spaces](https://github.com/Isthimius/Gondwana/wiki/Coordinate-Spaces)
- [Views and Cameras](https://github.com/Isthimius/Gondwana/wiki/Views-and-Cameras)
- [Engine Configuration](https://github.com/Isthimius/Gondwana/wiki/Engine-Configuration)

Relevant source files:

- [`Gondwana/Engine.cs`](https://isthimius.github.io/Gondwana/api/latest/Engine_8cs_source.html)
- [`Gondwana/CyclesPerSecondCalculatedEventArgs.cs`](https://isthimius.github.io/Gondwana/api/latest/CyclesPerSecondCalculatedEventArgs_8cs_source.html)
- [`Gondwana/Logging/EngineLogger.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineLogger_8cs_source.html)
- [`Gondwana/Logging/ModeLogger.cs`](https://isthimius.github.io/Gondwana/api/latest/ModeLogger_8cs_source.html)
- [`Gondwana/Rendering/RenderSurfaceHost.cs`](https://isthimius.github.io/Gondwana/api/latest/RenderSurfaceHost_8cs_source.html)
- [`Gondwana/Rendering/RefreshQueue.cs`](https://isthimius.github.io/Gondwana/api/latest/RefreshQueue_8cs_source.html)
- [`Gondwana/Rendering/Backbuffers/BackbufferBase.cs`](https://isthimius.github.io/Gondwana/api/latest/BackbufferBase_8cs_source.html)
- [`Gondwana/Scenes/Scene.cs`](https://isthimius.github.io/Gondwana/api/latest/Scene_8cs_source.html)
- [`Gondwana/Scenes/SceneLayer.cs`](https://isthimius.github.io/Gondwana/api/latest/SceneLayer_8cs_source.html)
