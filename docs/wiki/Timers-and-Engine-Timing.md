Gondwana uses a monotonic, high-resolution clock to measure elapsed real time and an engine-managed timer registry to schedule game work. These facilities live in the `Gondwana.Timers` namespace, but they serve different purposes:

- `HighResTimer` reads the underlying clock and converts raw timestamps into elapsed seconds.
- `Timer` raises one-shot or repeating `Tick` events at defined points in the engine cycle.

Use `HighResTimer` when code needs to *measure* time. Use `Timer` when code needs the engine to *schedule* an action. Neither API is a calendar clock, and `Timer` is not a separate worker thread.

## Table of contents

- [Timing at a glance](#timing-at-a-glance)
- [High-resolution time with HighResTimer](#high-resolution-time-with-highrestimer)
  - [Reading and comparing timestamps](#reading-and-comparing-timestamps)
  - [Ticks are not milliseconds](#ticks-are-not-milliseconds)
  - [Measuring elapsed work](#measuring-elapsed-work)
- [Scheduling work with Timer](#scheduling-work-with-timer)
  - [Create a repeating timer](#create-a-repeating-timer)
  - [Timers start when they are added](#timers-start-when-they-are-added)
  - [Choose PreCycle or PostCycle](#choose-precycle-or-postcycle)
  - [Choose Once or Repeating](#choose-once-or-repeating)
- [Interval accuracy, catch-up, and drift](#interval-accuracy-catch-up-and-drift)
- [Pausing timers](#pausing-timers)
- [Named timers and the timer registry](#named-timers-and-the-timer-registry)
- [Removing and disposing timers](#removing-and-disposing-timers)
- [Callback execution and thread safety](#callback-execution-and-thread-safety)
- [A complete timer-owning component](#a-complete-timer-owning-component)
- [Engine runtime and performance timing](#engine-runtime-and-performance-timing)
- [Common mistakes](#common-mistakes)
- [API quick reference](#api-quick-reference)
- [Source reference](#source-reference)

## Timing at a glance

| Requirement | Recommended API | Why |
| --- | --- | --- |
| Measure how long an operation took | `HighResTimer` | Reads a monotonic high-resolution clock |
| Run gameplay logic after a delay | `Timer` with `TimerCycles.Once` | Schedules the callback through the engine cycle |
| Run gameplay logic periodically | `Timer` with `TimerCycles.Repeating` | Maintains an interval without cumulative schedule drift |
| Update state before movement, collisions, and rendering | `TimerType.PreCycle` | Runs early in the background phase |
| Run diagnostics or cleanup after a rendered frame | `TimerType.PostCycle` | Runs at the end of the foreground phase |
| Read total engine runtime | `Engine.TotalSecondsEngineRunning` | Reports elapsed real time since the engine started |
| Observe update and render rates | `Engine.CyclesPerSecond`, `Engine.FramesPerSecond`, and `Engine.CPSCalculated` | Exposes sampled engine performance metrics |

Gondwana timing is based on elapsed real time. The timer system does not currently provide a simulated game clock, time scaling, or deterministic fixed-step scheduling. If a game needs slow motion, replayable simulation, or a pauseable world clock, build that layer on top of elapsed time rather than treating raw timer ticks as simulation steps.

## High-resolution time with HighResTimer

`HighResTimer` is a small wrapper around .NET's `System.Diagnostics.Stopwatch` timing source. It provides a monotonic timestamp suitable for duration measurements. Unlike `DateTime.Now`, it is not affected by clock corrections, time-zone changes, or daylight-saving transitions.

The class exposes four members:

| Member | Meaning |
| --- | --- |
| `TicksPerSecond` | Frequency of the underlying performance counter |
| `HighPerfSupported` | Whether `Stopwatch` reports a high-resolution performance counter |
| `GetCurrentTick()` | Current raw performance-counter timestamp |
| `GetDuration(start, stop)` | Elapsed seconds between two timestamps |
| `GetElapsedSince(start)` | Elapsed seconds from a timestamp until now |

### Reading and comparing timestamps

Store timestamps as `long` values. Subtract or compare timestamps from the same timing source; do not interpret them as dates.

```csharp
using Gondwana.Timers;

long startedAt = HighResTimer.GetCurrentTick();

LoadLevel();

float elapsedSeconds = HighResTimer.GetElapsedSince(startedAt);
Console.WriteLine($"Level loaded in {elapsedSeconds * 1000f:N1} ms");
```

`GetDuration` is useful when both endpoints are already available:

```csharp
long previousTick = HighResTimer.GetCurrentTick();

// Later...
long currentTick = HighResTimer.GetCurrentTick();
float deltaSeconds = HighResTimer.GetDuration(previousTick, currentTick);
previousTick = currentTick;
```

### Ticks are not milliseconds

A `HighResTimer` tick is one unit of the platform's performance counter. Its frequency is exposed by `HighResTimer.TicksPerSecond` and may vary by runtime or platform. It is not a `TimeSpan` tick, a millisecond, a frame, or an engine cycle.

Convert a raw interval to seconds using the API:

```csharp
float seconds = HighResTimer.GetDuration(startTick, stopTick);
```

Or perform the conversion explicitly when `double` precision is useful:

```csharp
double seconds = (stopTick - startTick)
    / (double)HighResTimer.TicksPerSecond;
```

Raw timestamps are process-local timing values. Do not persist them as save-game dates, send them as wall-clock times, or compare them with `DateTime` or Unix timestamps.

### Measuring elapsed work

`HighResTimer` is appropriate for profiling, cooldown calculations, interpolation, and other code that needs actual elapsed time without scheduling a callback.

```csharp
long pathfindingStarted = HighResTimer.GetCurrentTick();

Path path = pathfinder.FindPath(start, destination);

float pathfindingSeconds = HighResTimer.GetElapsedSince(pathfindingStarted);
if (pathfindingSeconds > 0.010f)
{
    Console.WriteLine(
        $"Pathfinding exceeded budget: {pathfindingSeconds * 1000f:N2} ms");
}
```

Measuring time does not involve the engine timer registry and does not raise an event. It simply reads the clock.

## Scheduling work with Timer

`Gondwana.Timers.Timer` is an engine-managed scheduler. A timer has:

- an interval, supplied in seconds;
- a phase, selected with `TimerType`;
- a lifecycle, selected with `TimerCycles`;
- a `Tick` event;
- an optional caller-supplied identifier;
- per-timer and global pause controls.

The name can conflict with other .NET timer types. An alias keeps examples and application code unambiguous:

```csharp
using Gondwana.Timers;
using EngineTimer = Gondwana.Timers.Timer;
```

### Create a repeating timer

Use `Timer.Add` to create and register a timer, then subscribe to `Tick`:

```csharp
private readonly EngineTimer _enemySpawnTimer;

public ArenaController()
{
    _enemySpawnTimer = EngineTimer.Add(
        timerID: "arena.enemy-spawn",
        type: TimerType.PreCycle,
        cycles: TimerCycles.Repeating,
        length: 2.0);

    _enemySpawnTimer.Tick += SpawnEnemy;
}

private void SpawnEnemy()
{
    // Runs on an eligible engine cycle, approximately every two seconds.
}
```

The `length` argument is expressed in seconds and accepts fractional values. For example, `0.25` means 250 milliseconds.

Timer intervals must be finite and positive, must convert to at least one high-resolution tick, and must fit within a positive Int64. Timer.Add throws ArgumentOutOfRangeException before registering the timer when these requirements are not met.

### Timers start when they are added

There is no separate `Start` method. `Timer.Add` records the current high-resolution timestamp, registers the timer, and begins its interval immediately.

Create timers during initialization when practical, and attach the handler immediately after `Add`. If the engine is already cycling on another thread, an extremely short interval could become due before later setup has finished.

If no stable identifier is needed, use the overload that generates a GUID string:

```csharp
EngineTimer timer = EngineTimer.Add(
    TimerType.PreCycle,
    TimerCycles.Once,
    length: 0.75);
```

Store the returned reference when the owning component will need to pause or dispose the timer later.

### Choose PreCycle or PostCycle

`TimerType` controls where Gondwana checks and raises a timer within an engine cycle.

| Type | Checked | Best suited for | Important consequence |
| --- | --- | --- | --- |
| `PreCycle` | Near the beginning of every background/update cycle | Gameplay state, AI decisions, spawning, cooldowns, scheduled movement changes | May run more often than rendered frames |
| `PostCycle` | At the end of a foreground/render cycle | Frame diagnostics, post-render bookkeeping, deferred cleanup | Runs only when the engine performs foreground work |

The relevant order is:

1. The engine obtains one current high-resolution timestamp for the cycle.
2. `PreCycle` timer events are raised.
3. Input is polled.
4. Animation and sprite movement advance.
5. Collisions are resolved.
6. Cameras update.
7. If the foreground frame is due, effects and direct drawings update.
8. The engine renders and presents eligible backbuffers.
9. `PostCycle` timer events are raised.

A `PreCycle` handler can therefore change game state in time for movement, collision handling, camera updates, and rendering in the same cycle. It is the normal choice for authoritative game behavior.

A `PostCycle` timer is tied to foreground cadence. When the engine's `Configuration.TargetFPS` setting throttles rendering, post-cycle timers are checked at that effective rate rather than at the potentially much higher background-cycle rate. Do not use `PostCycle` merely because an action is "later"; use it when the action genuinely belongs after foreground work.

### Choose Once or Repeating

`TimerCycles` controls whether the timer remains active after becoming due.

```csharp
EngineTimer closeGateTimer = EngineTimer.Add(
    "arena.close-gate",
    TimerType.PreCycle,
    TimerCycles.Once,
    length: 1.5);

closeGateTimer.Tick += CloseGate;
```

- `TimerCycles.Once` raises exactly one `Tick` event when it becomes due and is then removed from the active registry. Unlike a repeating timer, an overdue one-shot timer does not raise catch-up events. The timer is removed from the active registry during the processing pass in which it becomes due.
- `TimerCycles.Repeating` remains registered and continues to raise `Tick` until it is removed or disposed.

A one-shot timer is removed from the registry automatically, but registry removal is not the same as calling `Dispose` on a timer reference retained by application code. See [Removing and disposing timers](#removing-and-disposing-timers).

## Interval accuracy, catch-up, and drift

An engine timer does not interrupt the process at the exact instant its interval expires. Gondwana checks it at the next eligible engine phase:

- `PreCycle` timers are checked on background cycles.
- `PostCycle` timers are checked only on foreground cycles.

The callback can therefore be late by up to a cycle or frame under normal operation, and longer if the engine stalls. This is cooperative scheduling, not a real-time deadline system.

Repeating timers preserve their scheduled timeline. After a timer fires, Gondwana advances its internal timestamp by exactly one configured interval rather than resetting it to the current time. This avoids cumulative drift.

It also means that an overdue repeating timer catches up. If a 100 ms repeating timer is checked after 350 ms have elapsed, its handler can be invoked three times during the same engine pass. That behavior preserves the number of elapsed intervals, but it can produce a burst of work after a hitch.

Catch-up behavior applies only to `TimerCycles.Repeating`. A `TimerCycles.Once` timer raises no more than one callback, regardless of how many intervals have elapsed before it is checked.

Design repeating handlers accordingly:

- Keep callbacks short.
- Expect sequential catch-up calls with little or no real time between them.
- Avoid starting unbounded asynchronous work on every tick.
- Use `HighResTimer` separately when the handler needs to know actual elapsed time.
- Prefer a coarser interval or an elapsed-time accumulator for work that should be coalesced rather than replayed.

The timer's `Tick` event has no event arguments and does not supply delta time. A timer interval says when work is due; it does not stand in for the measured time step of a simulation.

## Pausing timers

Pause an individual timer through its `Paused` property:

```csharp
_enemySpawnTimer.Paused = true;

// Later...
_enemySpawnTimer.Paused = false;
```

Pause the entire timer registry through the static `PausedAll` property:

```csharp
EngineTimer.PausedAll = true;
```

While a timer is paused, Gondwana advances its internal baseline without raising `Tick`. Missed intervals are not replayed when the timer resumes. In the current implementation, resuming effectively begins a fresh full interval; partial progress accumulated before the pause is not retained.

`PausedAll` affects Gondwana `Timer` callbacks only. It does not stop the engine, stop rendering, freeze `HighResTimer`, or pause movement and animation automatically. A complete game-pause system must coordinate whichever subsystems the game considers pauseable.

## Named timers and the timer registry

Every timer is stored in a process-wide registry and has a string `TimerID`. Named IDs are useful for diagnostics and for systems that need to locate timers without retaining direct references.

```csharp
EngineTimer timer = EngineTimer.Get("arena.enemy-spawn");

int activeTimerCount = EngineTimer.Count;
string[] activeTimerIDs = EngineTimer.TimerIDs;
```

`Timer.Get` uses indexed lookup and throws if the ID is not registered. There is no `TryGet` API, so most components should retain their timer reference instead of repeatedly looking it up by name.

Treat caller-supplied IDs as unique. Adding another timer with an existing ID replaces the registry entry; it does not dispose the previously registered object. A component that deliberately reuses an ID should remove or dispose the existing timer first.

The registry is global rather than scene-specific. Loading a new scene does not automatically remove timers created by the old one. Scene controllers and other timer owners should clean up their own repeating timers.

## Removing and disposing timers

There are three principal cleanup operations:

```csharp
// Remove by identifier. If found, the timer is disposed.
EngineTimer.Remove("arena.enemy-spawn");

// Remove the exact timer and clear its Tick subscribers.
_enemySpawnTimer.Dispose();

// Dispose and remove every registered timer.
EngineTimer.ClearAll();
```

`Dispose` is idempotent. It removes the timer from the registry, clears its `Tick` subscribers, and prevents future engine-driven callbacks.

When a component owns a repeating timer, dispose it when the component is unloaded or disposed:

```csharp
public void Dispose()
{
    _enemySpawnTimer.Tick -= SpawnEnemy;
    _enemySpawnTimer.Dispose();
}
```

Explicitly unsubscribing is not required before `Dispose`, because disposal clears the event. Doing both can still make ownership obvious and remains safe.

A `Once` timer is automatically removed from the active registry after it becomes due. If application code retains a reference and wants deterministic event cleanup, it may still dispose that reference after the callback. An automatically removed timer cannot be restarted or re-added; create a new timer instead.

`Engine.Stop()` stops timer processing but does not clear the registry. `Engine.Dispose()` calls `Timer.ClearAll()`. If an application stops and later restarts the same engine session while retaining repeating timers, real time continues to advance and the timers can be overdue when processing resumes. Clear, dispose, or deliberately pause such timers as part of the application's stop/restart policy.

## Callback execution and thread safety

Timer callbacks execute synchronously inside the engine cycle. The engine does not queue each `Tick` to a thread-pool worker.

The actual thread depends on how the engine is hosted:

- With `Engine.Start`, the main cycle normally runs on the engine's background task.
- With `Engine.StartTimerDriven`, the host calls `Engine.Tick`, so callbacks run on that calling thread—commonly the UI thread in a single-threaded environment.

Do not assume that a timer callback is always a UI callback or always a background callback. Use the host's dispatching mechanism when touching thread-affine controls, and keep gameplay mutations consistent with the engine's normal ownership rules.

The static registry uses a concurrent collection, so adding and removing timers is safe across threads. Individual timer instances and application state modified by handlers are not automatically thread-safe.

Exceptions raised by a `Tick` handler are not isolated by `Timer`. An unhandled exception can escape into the engine cycle. Catch exceptions only where the application can handle them meaningfully; otherwise allow the normal application-level error policy to report and stop on a genuinely invalid state.

Avoid blocking work inside a handler. A slow `PreCycle` callback delays input, movement, collisions, and rendering. A slow `PostCycle` callback delays the next cycle. A repeating timer that is already catching up can magnify that cost.

## A complete timer-owning component

The following component owns one repeating gameplay timer and one replaceable one-shot timer. It uses explicit IDs, chooses `PreCycle` for game-state changes, and disposes everything it creates.

```csharp
using Gondwana.Timers;
using EngineTimer = Gondwana.Timers.Timer;

public sealed class EncounterController : IDisposable
{
    private readonly EngineTimer _spawnTimer;
    private EngineTimer? _gateTimer;
    private bool _disposed;

    public EncounterController()
    {
        _spawnTimer = EngineTimer.Add(
            "encounter.enemy-spawn",
            TimerType.PreCycle,
            TimerCycles.Repeating,
            length: 2.0);

        _spawnTimer.Tick += OnEnemySpawnDue;
    }

    public void CloseGateAfter(double delaySeconds)
    {
        if (delaySeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(delaySeconds));

        _gateTimer?.Dispose();

        _gateTimer = EngineTimer.Add(
            "encounter.close-gate",
            TimerType.PreCycle,
            TimerCycles.Once,
            delaySeconds);

        _gateTimer.Tick += OnGateCloseDue;
    }

    public void SetPaused(bool paused)
    {
        _spawnTimer.Paused = paused;

        if (_gateTimer is not null)
            _gateTimer.Paused = paused;
    }

    private void OnEnemySpawnDue()
    {
        SpawnEnemy();
    }

    private void OnGateCloseDue()
    {
        CloseGate();

        // Once removes the timer from the registry; Dispose also clears
        // the retained object's event subscription deterministically.
        _gateTimer?.Dispose();
        _gateTimer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _spawnTimer.Tick -= OnEnemySpawnDue;
        _spawnTimer.Dispose();

        if (_gateTimer is not null)
        {
            _gateTimer.Tick -= OnGateCloseDue;
            _gateTimer.Dispose();
            _gateTimer = null;
        }

        _disposed = true;
    }

    private void SpawnEnemy()
    {
        // Application-specific behavior.
    }

    private void CloseGate()
    {
        // Application-specific behavior.
    }
}
```

## Engine runtime and performance timing

The engine uses `HighResTimer` internally to advance animation, movement, camera state, effects, rendering cadence, and performance sampling. Applications can inspect several related values through `Engine.Instance`:

```csharp
double runningSeconds = Engine.Instance.TotalSecondsEngineRunning;
long runningTicks = Engine.Instance.TotalTicksEngineRunning;

double updateRate = Engine.Instance.CyclesPerSecond;
double foregroundRate = Engine.Instance.FramesPerSecond;
```

`CyclesPerSecond` counts all completed engine cycles. `FramesPerSecond` counts cycles that performed foreground work. The values are sampled rather than recalculated on every property access.

Subscribe to `CPSCalculated` for the complete sample:

```csharp
Engine.Instance.CPSCalculated += sample =>
{
    Console.WriteLine($"Update CPS: {sample.GrossCPS:N1}");
    Console.WriteLine($"Foreground FPS: {sample.NetCPS:N1}");

    if (sample.GpuFps is double gpuFps)
        Console.WriteLine($"GPU FPS: {gpuFps:N1}");
};
```

The sampling interval is controlled by `Engine.Instance.Configuration.SamplingTimeForCPS` and defaults to 1.5 seconds. Setting it to `0` disables sampling. `CPSCalculated` is posted through the engine's UI dispatcher, which differs from ordinary `Timer.Tick` execution.

On bitmap-backed surfaces, the foreground rate closely describes presented engine frames. GPU-backed surfaces can render on their graphics callback rather than directly on the background cycle; use `CyclesPerSecondCalculatedEventArgs.GpuFps` when an actual GPU frame count is available.

These values answer different questions:

- Low CPS indicates that background/update work is expensive or blocked.
- Healthy CPS with low foreground FPS can indicate rendering cost or foreground throttling.
- A low GPU FPS with a healthier scheduled foreground rate points toward the GPU render path, presentation, or vsync behavior.

They are diagnostics, not clocks for gameplay logic.

## Common mistakes

### Treating raw ticks as milliseconds

Always convert with `TicksPerSecond`, `GetDuration`, or `GetElapsedSince`. The raw frequency is platform-dependent.

### Passing an invalid interval

Timer lengths must be finite, positive, large enough to represent at least one high-resolution tick, and small enough to fit in Int64. Invalid lengths cause Timer.Add to throw ArgumentOutOfRangeException.

### Expecting exact wall-clock callback times

Timers are checked during eligible engine phases. A callback runs on the next applicable cycle, not through a separate interrupt at an exact deadline.

### Assuming one callback per engine pass

An overdue repeating timer can raise several catch-up callbacks in one pass.

### Using PostCycle for authoritative game state

`PostCycle` follows foreground cadence. Prefer `PreCycle` for state that must remain independent of the rendered frame rate.

### Doing expensive work inside Tick

Callbacks run synchronously and directly extend the cycle in which they execute.

### Forgetting global ownership

Timers are not owned automatically by a `Scene` or `SceneLayer`. Dispose repeating timers when their owning controller or game mode ends.

### Reusing an ID without cleanup

Adding a timer with an existing ID replaces the registry entry without disposing the old object. Remove or dispose the existing timer first.

### Assuming pause preserves partial progress

Paused timers do not catch up, and the current interval effectively restarts when processing resumes.

## API quick reference

### HighResTimer

```csharp
long HighResTimer.TicksPerSecond
bool HighResTimer.HighPerfSupported
long HighResTimer.GetCurrentTick()
float HighResTimer.GetDuration(long start, long stop)
float HighResTimer.GetElapsedSince(long start)
```

### Timer creation and registry

```csharp
EngineTimer.Add(string timerID, TimerType type, TimerCycles cycles, double length)
EngineTimer.Add(TimerType type, TimerCycles cycles, double length)
EngineTimer.Get(string timerID)
EngineTimer.Remove(string timerID)
EngineTimer.ClearAll()

int EngineTimer.Count
string[] EngineTimer.TimerIDs
bool EngineTimer.PausedAll
```

### Timer instance members

```csharp
event Action? Tick

TimerType Type
TimerCycles Cycles
long Length          // Stored internally in high-resolution ticks
string TimerID
bool Paused

void Dispose()
```

## Source reference

- [`Gondwana/Timers/HighResTimer.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Timers/HighResTimer.cs)
- [`Gondwana/Timers/Timer.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Timers/Timer.cs)
- [`Gondwana/Timers/TimerType.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Timers/TimerType.cs)
- [`Gondwana/Timers/TimerCycles.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Timers/TimerCycles.cs)
- [`Gondwana/Engine.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Engine.cs)
