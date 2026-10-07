Gondwana is designed to be inspectable while it runs. The engine exposes lifecycle events, render-surface events, runtime performance samples, logging infrastructure, and visual overlays that can help narrow a problem to a specific stage of the engine.

For opt-in collection, detached snapshots, and bounded history usable by ordinary
applications, see [Runtime Telemetry](Runtime-Telemetry). The saved-scene Scene Viewer's F3 diagnostics overlay—used by the `.gscn` editor/Studio workflow or direct Scene Viewer launches—consumes that same service; F3 is not a general Engine hotkey. The older CPS/FPS properties and event remain as warning-only obsolete compatibility APIs (`GOND0001`).

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
using Gondwana.Logging;
using Gondwana.Widgets.Hud;
using Microsoft.Extensions.Logging;

EngineLogger.SetLogLevel(LogLevel.Debug);

var diagnostics = new ProfilerWidget(
    host,
    view,
    new Rectangle(12, 12, 700, 520))
{
    ContextInfo = ProfilerContextInfo.All
};

diagnostics.Show();

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
| Desktop GPU backbuffer | Engine foreground work records an immutable `RenderFrameSnapshot`; the platform GL callback later replays the newest completed snapshot | Redraws the complete viewport and deliberately bypasses refresh queues |
| Browser WebGL backbuffer | `SKGLView` owns the browser paint callback, calls `Engine.Tick()`, and renders a new Scene synchronously when one is due; other browser paints can re-present the existing GPU backbuffer | Redraws the complete viewport for new Scene frames and deliberately bypasses refresh queues |

This distinction matters. A missing refresh rectangle can explain a bitmap-only artifact, but it cannot explain a GPU-only artifact because GPU rendering does not consume `RefreshQueue`.

For a desktop GPU problem, distinguish Engine-side snapshot production from platform GL replay. For a WebGL problem, inspect the synchronous browser paint path, Scene-render cost, canvas state, and presentation behavior. For a bitmap-only problem, invalidation and dirty-region projection belong much higher on the suspect list.

---

## Runtime telemetry: cycle, foreground, and presentation rates

For new diagnostics, use `Engine.Instance.Profiler`. It records neutral measurements
only while at least one collection request is active. A completed
`TelemetrySnapshot` includes an actual elapsed window, so cadence is derived as
sample count divided by `snapshot.ElapsedSeconds`.

The primary rate equivalents are:

| Question | Profiler measurement |
| --- | --- |
| How quickly is the simulation cycling? | `Engine [Core] / cycle.cpu.ms` count / elapsed seconds |
| How often is foreground work being produced? | `Engine [Core] / foreground.cpu.ms` count / elapsed seconds |
| How often is this GPU surface presenting? | render-source `presentation.count` count / elapsed seconds |

The same summaries also retain timing information: for example,
`cycle.cpu.ms.Mean` is average CPU duration per observed cycle, while its
`Count / ElapsedSeconds` is cycle cadence. Presentation is recorded per render
surface rather than being forced into the old cross-surface aggregate.

These rates answer different questions:

- a low cycle rate points toward expensive or blocked simulation/background work;
- a healthy cycle rate with a low foreground rate can indicate foreground pacing or rendering cost; and
- a low presentation rate with healthier foreground production points toward the GPU render/presentation path, VSync, the platform message loop, or compositor behavior.

The profiler is diagnostic instrumentation, not a gameplay clock.

### In-game runtime profiler widget

For an ordinary game, `Gondwana.Widgets` provides a view-level `ProfilerWidget`
that reads `Engine.Instance.Profiler` snapshots and formats them as a scrollable HUD:

```csharp
using System.Drawing;
using Gondwana.Widgets.Hud;

var diagnostics = new ProfilerWidget(
    host,
    view,
    new Rectangle(12, 12, 700, 520));

diagnostics.SetMeasurementVisible("layers.omitted", false);
diagnostics.Show();
```

Showing the widget acquires its own profiler collection request. Hiding or disposing
it releases only that request, so it can coexist with the saved-scene Scene Viewer's F3 diagnostics overlay or another
telemetry consumer. Individual measurements and entire sources can be shown or hidden,
and selected mode can be used as an explicit allow-list when a compact diagnostic view
is preferable.

See [[Runtime Telemetry]] and [[ProfilerWidget|Widgets---ProfilerWidget]] for the
measurement definitions and widget options.

### Scene Viewer F3 diagnostics (`.gscn` / Studio tooling)

The saved-scene **Scene Viewer** provides a built-in **F3 diagnostics overlay** for separating desktop GPU producer and consumer costs. This is the viewer opened by the standalone `.gscn` Scene editor/Studio **View Scene** workflow (and it can also be launched directly); F3 is therefore a Scene Viewer control, not a global Gondwana diagnostic shortcut. The overlay is implemented with `ProfilerWidget`, using its generic runtime-context options plus an extension callback for viewer-specific lines. It reports:

The Scene Viewer uses a curated default metric set and adds an explicit
`GPU FPS (presentation.count)` summary. Open **Metrics...** inside the overlay to
toggle individual sources or measurements, including lower-level/per-layer details
that are hidden by default. Hover a displayed measurement for its definition;
**Hover definitions** in the selector turns that help on or off. Scrolling over the
diagnostics panel scrolls its contents rather than changing the Scene Viewer camera
zoom.

- Gross CPS and Engine foreground FPS
- GPU presentation FPS
- snapshot build, query/sort, command-recording, and overlay-recording time
- GL replay CPU duration
- snapshot age from publication to acquisition
- published/replaced frame counts and occupied snapshot slots
- broad GL synchronization wait/held measurements

These values are particularly useful when Engine production is healthy but GL presentation falls behind. Snapshot replay can drop intermediate visual frames without blocking simulation. The GL replay duration measures CPU command replay and backbuffer flush time; it is not a direct measurement of GPU execution time.

See [[GL Rendering Path]] for the desktop snapshot diagnostics model.

### Legacy CPS/FPS compatibility surface

`Engine.CyclesPerSecond`, `Engine.FramesPerSecond`, `Engine.CPSCalculated`,
`EngineConfiguration.SamplingTimeForCPS`, and
`EngineConfiguration.SamplingTimeForCPSTicks` remain available for compatibility,
but are warning-only obsolete under `GOND0001`.

Existing handlers using `CyclesPerSecondCalculatedEventArgs` continue to work and
the DTO itself is intentionally not obsolete. New code should migrate to
`Engine.Profiler` or `ProfilerWidget`; see [[Runtime Telemetry]] for the exact
legacy-to-profiler mapping.


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
| `CPSCalculated` *(legacy / obsolete `GOND0001`)* | Consume compatibility cycle and rendering-rate samples; prefer `Engine.Profiler` |
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

`BeforeFrameRender` and `AfterFrameRender` bracket the engine's foreground phase, but their relationship to GPU presentation depends on the host.

- Bitmap backbuffers render inside that foreground phase.
- Desktop GPU hosts record the `RenderFrameSnapshot` during foreground work; the platform GL callback replays it later.
- Browser WebGL runs `Engine.Tick()` inside the `SKGLView` paint callback, and a due Scene frame is rendered synchronously after the foreground phase completes.

Consequently, the engine frame events do not measure final GPU presentation time. For desktop GPU work, use the snapshot/replay diagnostics to separate producer and consumer cost. For WebGL, profile the synchronous browser paint path. A GPU profiler is still required when actual device execution is the subject of the investigation.

---

## Render-surface instrumentation

Concrete `RenderSurfaceHost<TBackbuffer>` instances expose events around Scene rendering or recording. Their exact execution context depends on the rendering path.

| Event | Meaning |
| --- | --- |
| `RenderBackbufferBegin` | A Scene render/record operation has started |
| `RenderBackbufferEnd` | That Scene render/record operation has completed |
| `RenderBackbufferNoOp` | Bitmap rendering was skipped because the scene had no pending dirty work |
| `RenderBackbufferPostScene` | Scene content has been drawn to the active render or recording canvas |

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

Count or aggregate hot events instead of logging every invocation. `RenderBackbufferNoOp` is specific to the bitmap dirty-region path. GPU Scene renders are full-viewport rather than dirty-region operations; browser WebGL can also re-present the existing GPU backbuffer without performing a new Scene render.

### Threading

Render-surface hook behavior differs by path:

- bitmap hooks run on the engine thread against the CPU-backed render canvas
- desktop GPU hooks run on the engine thread while Gondwana records the `RenderFrameSnapshot`; the supplied canvas is a recording canvas, not a live GL surface
- browser WebGL hooks run synchronously inside the `SKGLView` paint callback while the WebGL context is current

A GPU surface creates its host when the control and adapter are initialized. Subscribe only after that initialization has occurred.

`RenderBackbufferPostScene` therefore has different resource rules on desktop GPU and WebGL. During desktop snapshot recording, draw only with CPU-safe resources and the supplied recording canvas; do not depend on `canvas.Surface`, a current `GRContext`, GPU textures, pixel readback, or other context-bound resources. In WebGL, the hook executes synchronously with the active context, so immediate GPU canvas work is legal, but context-bound resources must not be retained for later use.

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
| No profiler snapshots arrive | Confirm a `Profiler.Start()` request is active and enough time has elapsed for the configured telemetry interval |
| Cycle rate is high but animation looks slow | Compare foreground and presentation rates |
| GPU motion stutters while foreground production looks healthy | Compare the render source's `presentation.count` rate; on desktop inspect snapshot production versus GL replay, and on WebGL inspect the synchronous browser paint path |
| Logs appear late or out of order | Check whether asynchronous logging is active; temporarily use synchronous mode when ordering matters |
| Logs disappear under extreme volume | Check the enabled level and remember that a saturated asynchronous queue drops new records |
| Collision boxes do not appear | Verify layer selection, object visibility, and `CollisionsEnabled` |
| Collision boxes look correct but no collision occurs | Inspect collision type, profiles, groups, masks, and resolver behavior |
| A bitmap-rendered object changes state but leaves stale pixels | Force `Scene.FullRefreshNeeded`; if that fixes it, investigate invalidation |
| Full-buffer presentation fixes a bitmap artifact | Inspect screen-space dirty bounds and adapter partial presentation |
| GPU rendering works but bitmap rendering is stale | Investigate refresh queues and world-to-screen dirty-region projection |
| Bitmap rendering works but GPU rendering is wrong | Desktop: separate snapshot recording from GL replay; WebGL: inspect synchronous paint, canvas state, projection, and GPU-specific presentation behavior |
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
- [Bitmap Rendering Path](https://github.com/Isthimius/Gondwana/wiki/Bitmap-Rendering-Path)
- [GL Rendering Path](https://github.com/Isthimius/Gondwana/wiki/GL-Rendering-Path)
- [WebGL Rendering Path](https://github.com/Isthimius/Gondwana/wiki/WebGL-Rendering-Path)
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
