# Gondwana Engine Lifecycle

This document explains how a Gondwana game starts, runs, raises events, and shuts down. It is intended to be one place to answer **what runs when**, **on which thread**, and **which event or hook to use**.

This document covers the public runtime events exposed by the core `Gondwana` package and the lifecycle hooks in `Gondwana.Hosting` / `IEnginePlugin` that frame them.

---

## Contents

1. [Lifecycle at a glance](#1-lifecycle-at-a-glance)
2. [Threading model](#2-threading-model)
3. [Hosted startup sequence (`GameHostBase.Initialize`)](#3-hosted-startup-sequence-gamehostbaseinitialize)
4. [Engine initialization sequence (`Engine.Initialize`)](#4-engine-initialization-sequence-engineinitialize)
5. [Engine start sequence (`Engine.Start` / `StartTimerDriven`)](#5-engine-start-sequence-enginestart--starttimedriven)
6. [What happens every engine cycle](#6-what-happens-every-engine-cycle)
7. [Render-surface lifecycle](#7-render-surface-lifecycle)
8. [Shutdown and disposal sequence](#8-shutdown-and-disposal-sequence)
9. [Host hooks vs engine events vs plugin hooks](#9-host-hooks-vs-engine-events-vs-plugin-hooks)
10. [Complete public event reference](#10-complete-public-event-reference)
11. [Which hook should I use?](#11-which-hook-should-i-use)
12. [Final mental model](#12-final-mental-model)

---

## 1. Lifecycle at a glance

In a typical hosted game, the runtime sequence is:

1. Your UI creates a `GameHostBase` subclass.
2. `GameHostBase.Initialize()` configures platform adapters, input, content, scene graph, and the engine.
3. `Engine.Start(...)` binds the UI dispatcher and starts the main loop.
4. The engine repeats **background work** and **foreground/render work** until stopped.
5. `GameHostBase.Dispose()` stops the engine and disposes the singleton.

Two important rules shape everything else:

- **Normal mode:** `Engine.Start(SynchronizationContext)` runs the main `Cycle()` loop on a background task.
- **Timer-driven mode:** `Engine.StartTimerDriven(SynchronizationContext)` does not create a background task; the caller must drive the engine by calling `Tick()`.

---

## 2. Threading model

| Area | Normal startup | Timer-driven startup |
| --- | --- | --- |
| `GameHostBase.Initialize()` | Calling thread, usually the UI thread | Calling thread, usually the UI thread |
| `Engine.Initialize()` | Calling thread unless `UiDispatcher` is already assigned | Calling thread unless `UiDispatcher` is already assigned |
| Main `Cycle()` loop | Engine background thread | Calling thread that invokes `Tick()` |
| Input poller events | Engine thread | Same thread as `Tick()` |
| `BeforeBackgroundTasksExecute` / `AfterBackgroundTasksExecute` | Engine thread | Same thread as `Tick()` |
| `BeforeFrameRender` / `AfterFrameRender` | Engine thread | Same thread as `Tick()` |
| `RenderBackbufferPostScene` / `IEnginePlugin.OnPostRenderCanvas` | Engine thread (CPU surfaces); GL thread (GPU surfaces) | Engine/tick thread (CPU); GL thread (GPU) |
| `CPSCalculated` | Posted to `UiDispatcher` | Posted to `UiDispatcher` |
| `Disposing` / `Disposed` on `Engine` | Posted to `UiDispatcher` when available | Posted to `UiDispatcher` when available |

### Important nuance: initialization events can be inline or dispatched

`PreInitialization`, `PostInitialization`, and `InitializationComplete` behave differently depending on **when** `Initialize()` is called:

- If `GameHostBase.Initialize()` calls `Engine.Initialize()` first, `UiDispatcher` is not set yet, so those events run **inline on the calling thread**.
- If something calls `Engine.Start(...)` first and lets `Start(...)` auto-initialize the engine, `UiDispatcher` is already available, so those events are **posted to the UI dispatcher**.

The standard `GameHostBase` flow uses the first case.

---

## 3. Hosted startup sequence (`GameHostBase.Initialize`)

The authoritative host order today is:

```text
Initialize()
  → ConfigureLogging
  → ConfigurePlatform
  → ConfigureInput
       → ConfigureKeyboard
       → ConfigureMouse
       → ConfigureGamepads
       → ConfigureTouch
  → InitializeGameContent
       → LoadContent
            → LoadAssets
            → LoadTilesheets
            → LoadAnimationCycles
       → CreateSceneGraph
            → CreateInitialScene
            → CreateInitialViews
       → BindScene
       → InitializeSceneObjects
            → CreateSprites
            → CreateDirectDrawings
  → InitializeEngine
  → StartEngine
       → OnStartEngine
```

### What each stage is for

| Stage | Purpose |
| --- | --- |
| `ConfigureLogging` | Sets engine logging verbosity before runtime work begins. |
| `ConfigurePlatform` | Wires render surfaces and platform-specific adapters. |
| `ConfigureInput` | Supplies keyboard, mouse, touch, and gamepad adapters/configuration. |
| `InitializeGameContent` | Loads assets, creates the scene graph, binds the scene, and creates runtime objects. |
| `InitializeEngine` | Loads engine configuration/state and raises engine initialization events. |
| `StartEngine` | Captures the UI `SynchronizationContext`, starts the engine loop, then calls `OnStartEngine`. |

### Why the host does content setup before `Engine.Initialize`

This is intentional in the current implementation. A host can construct scenes, views, sprites, and direct drawings before the engine loop starts. `Engine.Initialize()` is the point where engine configuration/state loading and input poller initialization are finalized.

---

## 4. Engine initialization sequence (`Engine.Initialize`)

When `Engine.Initialize(...)` runs, the ordered flow is:

1. Return immediately if the engine is already initialized or currently initializing.
2. Raise `PreInitialization`.
3. Load `EngineConfiguration` from disk.
4. Apply logging mode.
5. Merge any configured `EngineState` files.
6. Initialize keyboard and mouse pollers if adapters were supplied.
7. Store the touch adapter and gamepad manager.
8. Raise `PostInitialization`.
9. Invoke registered plugin `OnInitialize` hooks.
10. Mark `IsInitialized = true`.
11. Raise `InitializationComplete`.

### Initialization event order

| Order | Event / hook | Notes |
| --- | --- | --- |
| 1 | `Engine.PreInitialization` | Earliest public engine event. |
| 2 | `Engine.PostInitialization` | Raised after config/state/input setup completes. |
| 3 | `IEnginePlugin.OnInitialize` | Plugin hook, not a C# event. |
| 4 | `Engine.InitializationComplete` | Final initialization signal. |

---

## 5. Engine start sequence (`Engine.Start` / `StartTimerDriven`)

### `Engine.Start(SynchronizationContext)`

1. Return if already running.
2. Create `UiDispatcher` from the supplied `SynchronizationContext`.
3. Ensure initialization has completed (wait, or initialize now).
4. Mark `IsRunning = true`.
5. Capture start/sampling ticks.
6. Launch the engine loop on a background task.
7. Bind `EngineDispatcher` to that engine thread.
8. Repeatedly call `Cycle()` until `IsRunning` becomes `false`.

### `Engine.StartTimerDriven(SynchronizationContext)`

1. Return if already running.
2. Create `UiDispatcher`.
3. Initialize immediately if needed.
4. Bind `EngineDispatcher` to the calling thread.
5. Mark `IsRunning = true`.
6. Return; the caller now owns the loop by calling `Tick()`.

Use timer-driven mode for single-threaded environments such as WebAssembly.

---

## 6. What happens every engine cycle

The `Cycle()` method is the heart of Gondwana. In normal mode it runs on the engine thread; in timer-driven mode it runs wherever `Tick()` is called.

### Per-cycle order

```text
Cycle()
  → EngineDispatcher.Drain()
  → IEnginePlugin.OnPreCycle
  → DoBackgroundTasks
       → BeforeBackgroundTasksExecute
       → Timer.RaiseTimerEvents(PreCycle)
       → Keyboard poll
       → Mouse poll
       → Touch poll
       → Gamepad poll
       → Tile animation advance
       → Sprite movement advance
       → Collision resolution
       → Camera updates
       → AfterBackgroundTasksExecute
  → [if a frame is due]
       → IEnginePlugin.OnPreFrameRender
       → DoForegroundTasks
            → BeforeFrameRender
            → DirectDrawingManager.UpdateAll
            → RenderSurfaceHost.RenderToBackbuffer (CPU-backed surfaces)
            → RenderSurfaceHost.RenderBackbufferPostScene (CPU-backed surfaces, if scene was drawn)
            → IEnginePlugin.OnPostRenderCanvas (CPU-backed surfaces, if scene was drawn)
            → PresentBackbufferToAdapter (CPU-backed surfaces)
            → Gamepad manager state update
            → AfterFrameRender
            → Timer.RaiseTimerEvents(PostCycle)
       → IEnginePlugin.OnPostFrameRender
  → [if CPS sampling is enabled]
       → CalculateCPS
            → Engine.CPSCalculated (posted to UI dispatcher)
  → IEnginePlugin.OnPostCycle
```

### Background phase details

The background phase is where Gondwana advances simulation-oriented work:

- pre-cycle timers fire
- input devices are polled
- tile animations advance
- sprite movement paths advance
- collision resolvers run
- cameras update and can force a full refresh

If you need a hook that runs **every cycle even when no frame is rendered**, use:

- `BeforeBackgroundTasksExecute`
- `AfterBackgroundTasksExecute`
- `IEnginePlugin.OnPreCycle`
- `IEnginePlugin.OnPostCycle`

### Foreground phase details

The foreground phase is throttled by `EngineConfiguration.TargetFPS` unless that value is `0` or less.

During this phase Gondwana:

- raises `BeforeFrameRender`
- updates retained direct drawings
- renders/presents non-GL backbuffers
- updates gamepad state snapshots
- raises `AfterFrameRender`
- fires post-cycle timers

If you need work that tracks the actual rendered frame cadence, use:

- `BeforeFrameRender`
- `AfterFrameRender`
- `IEnginePlugin.OnPreFrameRender`
- `IEnginePlugin.OnPostFrameRender`

### CPS/FPS sampling

`CPSCalculated` is raised only when `Configuration.SamplingTimeForCPS > 0` and the configured sampling interval has elapsed. The payload includes:

- gross cycle count / CPS
- net rendered frame count / FPS
- elapsed sample duration
- GPU FPS, when GPU backbuffers are present

---

## 7. Render-surface lifecycle

A `RenderSurfaceHost<TBackbuffer>` sits between a scene, a `ViewManager`, a backbuffer, and a platform adapter.

Typical render-surface flow:

1. A host creates the render surface adapter.
2. `Bind(scene)` attaches the scene and raises `BindToScene`.
3. `ViewManager` manages one or more `View` instances.
4. When a frame is rendered, `RenderSurfaceHost` raises `RenderBackbufferBegin`.
5. If the scene is clean, it raises `RenderBackbufferNoOp` and returns.
6. Otherwise it redraws dirty content, raises `RenderBackbufferPostScene` (and invokes
   `IEnginePlugin.OnPostRenderCanvas` on all plugins), then raises `RenderBackbufferEnd`.
7. Adapter resize changes raise `RenderSurfaceAdapterBase.Resized`.

View-related lifecycle signals:

- `ViewManager.ViewAdded` / `ViewRemoved`
- `Viewport.TargetRectChanged`
- `Viewport.ZoomChanged`

Scene-related changes can also force refreshes that show up during later render passes.

---

## 8. Shutdown and disposal sequence

In the standard hosted path, disposal starts at the host:

```text
GameHostBase.Dispose()
  → UnhookEvents
  → StopEngine
       → Engine.Stop()
  → DisposeEngine
       → Engine.Dispose()
```

### `Engine.Stop()`

`Stop()` only ends the loop:

1. Return if not running.
2. Set `IsRunning = false`.
3. Schedule plugin `OnShutdown` to run after the cycle task exits, or run it immediately if there is no active cycle task.

### `Engine.Dispose()`

`Dispose()` performs full teardown:

1. Mark `IsDisposing = true`.
2. Call `Stop()`.
3. Wait for the cycle task to exit.
4. Raise `Disposing`.
5. Stop keyboard, mouse, touch, and gamepad monitoring.
6. Flush async logs if configured.
7. Clear timers.
8. Clear engine state.
9. Mark `IsDisposed = true`.
10. Raise `Disposed`.

### Shutdown rules to remember

- `Stop()` does **not** dispose resources.
- `Dispose()` does **not** restart the singleton; after disposal, treat the engine as finished for the process lifetime.
- `Disposing` and `Disposed` are only raised for explicit disposal, not the finalizer path.

---

## 9. Host hooks vs engine events vs plugin hooks

Gondwana exposes lifecycle extension points in three different forms:

| Type | Examples | Use when |
| --- | --- | --- |
| Host virtual methods | `ConfigurePlatform`, `LoadAssets`, `CreateSprites`, `OnStartEngine` | You are building a custom `GameHostBase` subclass. |
| Engine/runtime events | `InitializationComplete`, `BeforeFrameRender`, `SceneLayerAdded`, `KeyDown` | You want subscription-based notifications from runtime objects. |
| Plugin hooks | `OnInitialize`, `OnPreCycle`, `OnShutdown` | You want engine-wide cross-cutting behavior without wiring many event subscriptions. |

A good rule of thumb:

- Use **host overrides** for one-time composition/setup.
- Use **engine events** for object-specific runtime reactions.
- Use **plugins** for engine-wide instrumentation or framework extensions.

---

## 10. Complete public event reference

The tables below group the public runtime events exposed by the core `Gondwana` assembly.

### 10.1 Engine lifecycle and loop events

| Event | Raised when | Typical thread |
| --- | --- | --- |
| `Engine.PreInitialization` | Immediately before engine initialization work begins | Calling thread or UI dispatcher |
| `Engine.PostInitialization` | After config/state/input setup completes | Calling thread or UI dispatcher |
| `Engine.InitializationComplete` | At the end of successful initialization | Calling thread or UI dispatcher |
| `Engine.BeforeBackgroundTasksExecute` | At the start of each background phase | Engine thread |
| `Engine.AfterBackgroundTasksExecute` | At the end of each background phase | Engine thread |
| `Engine.BeforeFrameRender` | Right before foreground/render work | Engine thread |
| `Engine.AfterFrameRender` | Right after foreground/render work | Engine thread |
| `Engine.CPSCalculated` | When CPS/FPS sampling is computed | UI dispatcher |
| `Engine.Disposing` | When explicit engine disposal starts | UI dispatcher if available |
| `Engine.Disposed` | After explicit engine disposal completes | UI dispatcher if available |

### 10.2 Plugin lifecycle hooks (not C# events)

| Hook | Called when |
| --- | --- |
| `IEnginePlugin.OnInitialize` | During `Engine.Initialize`, after `PostInitialization` |
| `IEnginePlugin.OnPreCycle` | At the start of each engine cycle |
| `IEnginePlugin.OnPreFrameRender` | Right before the foreground/render phase |
| `IEnginePlugin.OnPostFrameRender` | Right after the foreground/render phase |
| `IEnginePlugin.OnPostRenderCanvas` | After all scene content is drawn to a surface's backbuffer canvas, once per surface per frame (not called when frame is skipped or surface has no views) |
| `IEnginePlugin.OnPostCycle` | At the end of each engine cycle |
| `IEnginePlugin.OnShutdown` | After the engine loop has stopped |

### 10.3 Timers and input events

| Event | Raised when | Notes |
| --- | --- | --- |
| `Timer.Tick` | A timer interval elapses | Fired from pre-cycle or post-cycle timer processing, depending on timer type |
| `KeyboardEventPoller.KeyDown` | A monitored key is pressed, repeated, or released | `KeyAction` indicates which transition occurred |
| `MouseEventPoller.MouseEvent` | Mouse movement/button/scroll activity matches poller config | Single consolidated mouse event stream |
| `GamepadEventPoller.ButtonDown` | A monitored gamepad button is pressed and throttle rules allow it | Only button-down events are exposed |
| `TouchEventPoller.TouchBegan` | A new touch contact is observed | Engine-thread touch polling event |
| `TouchEventPoller.TouchMoved` | An existing touch contact changes position | Engine-thread touch polling event |
| `TouchEventPoller.TouchEnded` | A tracked touch contact ends or is cancelled | Ended contacts are drained promptly even when throttled |
| `TouchEventPoller.TouchEvent` | A gesture recognizer emits tap/swipe/pinch output | Consolidated gesture stream |
| `TapGestureRecognizer.Tapped` | A qualifying tap completes | Single-finger tap recognizer |
| `SwipeGestureRecognizer.Swiped` | A qualifying swipe completes | Single-finger swipe recognizer |
| `PinchGestureRecognizer.PinchUpdated` | Distance between two active touches changes | Emits incremental pinch/spread updates |

### 10.4 Scene, layer, view, and render-surface events

| Event | Raised when | Notes |
| --- | --- | --- |
| `Scene.SceneLayerAdded` | A layer is added to a scene | After internal scene bookkeeping completes |
| `Scene.SceneLayerRemoved` | A layer is removed from a scene | After internal cleanup completes |
| `Scene.SceneDisposing` | A scene starts disposing | Raised before layers are removed |
| `SceneLayer.SceneLayerTileSizeChanged` | Tile width/height changes | Usually implies full refresh |
| `SceneLayer.VisibleChanged` | Layer visibility changes | Affects visible layer set |
| `SceneLayer.WrappingChanged` | Horizontal/vertical wrapping changes | Usually implies full refresh |
| `SceneLayer.ShowGridLinesChanged` | Grid-line debug display changes | Usually implies full refresh |
| `SceneLayer.ShowCollisionBoxesChanged` | Collision-box debug display changes | Usually implies full refresh |
| `SceneLayer.ZOrderChanged` | Layer z-order changes | Can trigger resorting |
| `SceneLayer.ParallaxChanged` | Layer parallax changes | Usually implies full refresh |
| `SceneLayer.OriginPxChanged` | Layer origin changes | Usually implies full refresh |
| `SceneLayer.Disposing` | A layer starts disposing | Raised before tiles/resources are released |
| `ViewManager.ViewAdded` | A view is added to a render surface | Render surface marks scene for refresh |
| `ViewManager.ViewRemoved` | A view is removed from a render surface | Render surface marks scene for refresh |
| `Viewport.TargetRectChanged` | A viewport rectangle changes | Resize or move of the viewport |
| `Viewport.ZoomChanged` | A viewport zoom changes | Can force a full refresh |
| `RenderSurfaceAdapterBase.Resized` | The platform render surface changes size | Backbuffer recreation usually follows |
| `RenderSurfaceHost.BindToScene` | A render surface binds to a scene | Includes old/new scene info |
| `RenderSurfaceHost.RenderBackbufferBegin` | A backbuffer render starts | Render-surface level render hook |
| `RenderSurfaceHost.RenderBackbufferEnd` | A backbuffer render finishes | Render-surface level render hook |
| `RenderSurfaceHost.RenderBackbufferPostScene` | All scene content for the frame has been drawn; canvas is ready for post-scene effects | Fires before `RenderBackbufferEnd`; not raised when frame is skipped or surface has no views. On CPU/bitmap surfaces using dirty-rectangle presentation (`RedrawDirtyRectangleOnly`), post-scene drawing that changes pixels outside the tracked dirty rect may not be presented; expand the dirty region or disable dirty-rect presentation for full-surface effects. |
| `RenderSurfaceHost.RenderBackbufferNoOp` | A render is skipped because nothing is dirty | Useful for diagnostics |

### 10.5 Sprite, movement, and animation events

| Event | Raised when | Notes |
| --- | --- | --- |
| `SpriteManager.SpriteCreated` | A new sprite is created | Raised by `CreateSprite` |
| `Sprite.SpriteMoved` | A sprite changes position | Movement/location event |
| `Sprite.Disposing` | A sprite begins disposing | Cleanup hook |
| `Sprite.ResizeComplete` | A resize/pulse leg completes or is cancelled | Covers both explicit resize and pulse behavior |
| `MovementController.ScriptedMovementStarted` | A scripted movement begins | Tween / `MoveTo` / `MoveToward` start |
| `MovementController.ScriptedMovementStopped` | A scripted movement ends or is cancelled | Final scripted movement signal |
| `Animator.Started` | Animation playback starts | Tile animator event |
| `Animator.Stopped` | Animation playback stops | Tile animator event |
| `Animator.Cycled` | Animation advances to the next frame | Per-cycle animation event |

### 10.6 Direct drawing, tilesheet, and audio events

| Event | Raised when | Notes |
| --- | --- | --- |
| `DirectDrawingBase.Disposing` | A direct drawing starts disposing | Base direct-drawing cleanup event |
| `DirectDrawingBase.FadeToCompleted` | A fade transition reaches its target opacity | Raised on the completion frame |
| `DirectComposite.Disposing` | A direct composite starts disposing | Composite-specific cleanup event |
| `TextBlock.TextRevealed` | More text becomes visible during a reveal animation | Argument is current revealed text |
| `TextBlock.TextRevealComplete` | A text reveal finishes | Argument is the full text |
| `Tilesheet.Disposed` | A tilesheet is disposed | Asset cleanup event |
| `AudioResource.PlaybackCompleted` | Non-looping audio playback finishes | Playback completion event |
| `AudioResource.Disposed` | An audio resource is disposed | Asset cleanup event |
| `AudioResourceManager.SoundDisposed` | A managed sound resource is disposed | Includes key/resource pair |

---

## 11. Which hook should I use?

| You want to... | Use |
| --- | --- |
| Run one-time setup before the engine starts | `GameHostBase` overrides |
| Observe engine initialization completion | `Engine.InitializationComplete` |
| Run logic every cycle, even if a frame is skipped | `BeforeBackgroundTasksExecute` / `AfterBackgroundTasksExecute` or plugin cycle hooks |
| Run logic only when a frame is actually rendered | `BeforeFrameRender` / `AfterFrameRender` |
| Draw post-scene effects onto a specific surface's canvas (overlays, color grading, etc.) | `RenderSurfaceHost.RenderBackbufferPostScene` |
| Draw post-scene effects onto every surface's canvas from a plugin | `IEnginePlugin.OnPostRenderCanvas` |
| React to keyboard/mouse/touch/gamepad input | Input poller events |
| Track scene/view/render-surface composition changes | Scene / `ViewManager` / render-surface events |
| Track animation or scripted movement completion | `Animator` and `MovementController` events |
| Instrument the whole engine without wiring many subscriptions | `IEnginePlugin` hooks |

---

## 12. Final mental model

If you remember only one diagram, make it this one:

```text
UI Thread
  └─ GameHostBase.Initialize()
       ├─ platform/input/content composition
       ├─ Engine.Initialize()
       └─ Engine.Start()

Engine Thread (normal mode)
  └─ Cycle()
       ├─ plugins pre-cycle
       ├─ background work + input + movement + collisions
       ├─ optional frame render
       │    ├─ BeforeFrameRender
       │    ├─ RenderToBackbuffer (CPU surfaces) → RenderBackbufferPostScene
       │    └─ AfterFrameRender
       ├─ optional CPS sample
       └─ plugins post-cycle

GL Thread (GPU surfaces only)
  └─ PaintSurface (driven by SKGLControl)
       ├─ GlRenderAndSnapshot
       │    ├─ RenderToBackbuffer → RenderBackbufferPostScene
       │    └─ Snapshot
       └─ blit to window surface

Shutdown
  └─ GameHostBase.Dispose()
       ├─ Engine.Stop()
       └─ Engine.Dispose()
```

That is the Gondwana lifecycle in practice: **host composes**, **engine cycles**, **events signal phase changes**, and **disposal shuts the whole graph down in order**.
