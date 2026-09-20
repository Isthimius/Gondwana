Gondwana uses the standard `Microsoft.Extensions.Logging` abstractions and routes engine and game logs through `EngineLogger`.

For most games, the useful setup is straightforward:

1. choose the minimum log level when the game host starts
2. obtain a typed logger for each major game class or subsystem
3. write structured messages with named values
4. leave asynchronous logging enabled during normal play
5. temporarily use synchronous logging only when exact ordering matters

This article covers the game-developer-facing logging workflow. For using logs alongside runtime metrics, collision overlays, and render diagnostics, see [Debugging and Instrumentation](https://github.com/Isthimius/Gondwana/wiki/Debugging-and-Instrumentation).

---

## Quick start

When using a Gondwana game host, select the log level as part of host initialization:

```csharp
using Microsoft.Extensions.Logging;

_gameHost.Initialize(logLevel: LogLevel.Information);
```

The generated WinForms, Avalonia, and Blazor templates all initialize their hosts this way. The default level is `LogLevel.Warning`, which keeps normal output quiet.

Inside game code, request a typed logger from `EngineLogger`:

```csharp
using Gondwana.Logging;
using Microsoft.Extensions.Logging;

internal sealed class PlatformerGameHost
{
    private static ILogger<PlatformerGameHost> Log =>
        EngineLogger.GetLogger<PlatformerGameHost>();

    private void LoadLevel(string levelName)
    {
        Log.LogInformation(
            "Loading level {LevelName}",
            levelName);
    }
}
```

The logger category is the full name of `PlatformerGameHost`, making messages from different game systems easier to filter.

The property form above is intentional. `EngineLogger.SetLogLevel` replaces the underlying logger factory and clears Gondwana's logger cache. Looking up the typed logger again ensures that a runtime level change uses the current factory; a logger instance cached before that change remains attached to the older factory.

---

## Choosing a log level

The minimum level controls which messages reach the configured providers.

| Level | Appropriate game usage |
| --- | --- |
| `Trace` | Extremely detailed, temporary evidence such as coordinate conversions, individual input transitions, or a narrowly scoped collision investigation |
| `Debug` | Development-time state changes, spawning, scene transitions, AI decisions, and diagnostic summaries |
| `Information` | Normal milestones worth retaining, such as game startup, level loading, save completion, or a player joining a session |
| `Warning` | Unexpected but recoverable conditions, such as a missing optional asset or a fallback configuration being used |
| `Error` | An operation failed and the game could not complete it as intended |
| `Critical` | The game or a major subsystem cannot continue safely |

For ordinary development, `Debug` or `Information` is usually useful. For a normal release build, `Warning` is a sensible starting point.

```csharp
_gameHost.Initialize(logLevel: LogLevel.Debug);
```

Avoid enabling `Trace` globally unless the investigation is short and focused. A log entry on every engine cycle, tile, particle, or collision candidate can create the performance problem you are attempting to measure.

---

## Engine logs and game logs

`Engine.Logger` is Gondwana's logger for the `Engine` category:

```csharp
Engine.Logger.LogInformation("Engine diagnostics are enabled");
```

Game code may use it for a quick experiment, but typed game loggers are preferable for permanent messages:

```csharp
private static ILogger<EnemyDirector> Log =>
    EngineLogger.GetLogger<EnemyDirector>();
```

Categories answer *which subsystem produced this message?* A useful game might have separate categories for:

- its game host
- level loading
- player state
- enemy behavior
- saving and persistence
- networking
- custom tooling

Do not create a different logger for every object instance. Use the containing class or subsystem as the category and include the instance identity as structured data.

---

## Write structured messages

Use message templates with named placeholders:

```csharp
Log.LogDebug(
    "Player {PlayerName} moved from {OldPosition} to {NewPosition}",
    playerName,
    oldPosition,
    newPosition);
```

This is better than interpolation:

```csharp
// Avoid for permanent logging.
Log.LogDebug($"Player {playerName} moved from {oldPosition} to {newPosition}");
```

The template preserves `PlayerName`, `OldPosition`, and `NewPosition` as named values for providers that support structured data. It also avoids eagerly formatting the complete message before the logger decides whether `Debug` is enabled.

Use stable, descriptive placeholder names. Prefer `{LevelName}` over `{Value}` and `{CollisionType}` over `{Thing}`.

### Expensive diagnostic values

Message templates do not prevent game code from calculating an expensive argument before the logging call. Check the enabled level first when producing a dump, enumeration, or other costly value:

```csharp
if (Log.IsEnabled(LogLevel.Trace))
{
    string snapshot = BuildDetailedWorldSnapshot();

    Log.LogTrace(
        "World snapshot: {WorldSnapshot}",
        snapshot);
}
```

For inexpensive values such as identifiers, positions, counters, and enum values, the explicit check is usually unnecessary.

---

## Log exceptions with context

Pass the exception separately and explain which operation failed:

```csharp
try
{
    LoadLevel(levelPath);
}
catch (Exception ex)
{
    Log.LogError(
        ex,
        "Could not load level {LevelPath}",
        levelPath);

    throw;
}
```

The message should add information that the exception does not already contain. “An error occurred” is technically true and practically decorative.

Choose the level based on the effect:

- use `Warning` when the game recovered and selected a valid fallback
- use `Error` when the requested operation failed
- use `Critical` when continuing would leave the game in an invalid or unsafe state

Avoid logging the same exception at every layer of the call stack. Log it where the game has enough context to describe the failed operation, or where it is finally handled.

---

## Use event IDs for recurring game events

EngineLogger preserves the standard EventId supplied through ILogger in both synchronous and asynchronous logging modes. Event IDs are useful when the same kind of event appears across several classes or when a logging provider or downstream sink will group or filter entries programmatically.

```csharp
private static class GameLogEvents
{
    internal static readonly EventId LevelLoaded =
        new(1001, nameof(LevelLoaded));

    internal static readonly EventId SaveFailed =
        new(2001, nameof(SaveFailed));
}
```

Use the event ID in the logging call:

```csharp
Log.LogInformation(
    GameLogEvents.LevelLoaded,
    "Loaded level {LevelName} in {ElapsedMilliseconds:F1} ms",
    levelName,
    elapsedMilliseconds);
```

Event IDs are optional. Add them where stable classification provides value; do not build a miniature bureaucracy for three startup messages.

---

## Asynchronous logging

Gondwana uses asynchronous logging by default. Each enabled record is placed into a bounded channel and written by a background worker.

This protects the engine thread from a slow console or logging provider, but it has deliberate tradeoffs:

- messages may appear slightly after the code that produced them
- exact ordering relative to breakpoints or UI activity can be harder to observe
- logging scopes do not reliably cross the background-thread boundary
- when the queue is full, new records are dropped rather than blocking gameplay

The default queue capacity is 8192 records. Configure the mode and capacity through the engine configuration loaded during initialization:

| Setting | Default | Meaning |
| --- | --- | --- |
| `LoggingMode` | `Asynchronous` | Selects asynchronous or synchronous delivery |
| `LoggingQueueCapacity` | `8192` | Maximum queued records in asynchronous mode |
| `FlushAsyncLogsOnShutdown` | `true` | Requests a best-effort flush when the engine is disposed |

Queue saturation is intentionally silent. `EngineLogger.LoggingError` reports failures in asynchronous logging operations; it does not report records dropped because the queue was full.

If missing records are possible only during an intense burst of `Trace` output, the correct answer is usually to reduce or aggregate the logging rather than to make the queue enormous.

---

## Synchronous logging for a focused investigation

Synchronous logging writes immediately on the calling thread. It is useful when:

- exact message ordering matters
- a breakpoint must correspond directly to the most recent log entry
- normal logging scopes are required
- the logging pipeline itself is being diagnosed

Switch at runtime with the convenience method that first attempts to drain pending asynchronous records:

```csharp
EngineLogger.SwitchToSyncAndFlush();

try
{
    ReproduceTheProblem();
}
finally
{
    EngineLogger.SwitchToAsync();
}
```

The flush is best effort and uses a timeout. Synchronous logging can block the engine thread while providers write, so it should not be used to measure normal game-loop performance.

Prefer `SwitchToSyncAndFlush()` over assigning `EngineLogger.Mode` directly when leaving asynchronous mode. The convenience method handles the existing background pipeline and its queued records.

If synchronous behavior is required for the complete run, select `Synchronous` in the engine configuration rather than switching after startup.

---

## Changing the log level at runtime

`EngineLogger.SetLogLevel` changes the global minimum level used by Gondwana's built-in logger factory:

```csharp
EngineLogger.SetLogLevel(LogLevel.Trace);
```

This can support a development-only menu, console command, or hotkey:

```csharp
private void EnableVerboseDiagnostics()
{
    EngineLogger.SetLogLevel(LogLevel.Trace);
    Log.LogInformation("Verbose diagnostics enabled");
}
```

Changing the level recreates the logger factory and clears Gondwana's logger cache. Retrieve typed loggers through `EngineLogger.GetLogger<T>()` again after the change. The logger-property pattern used earlier does this naturally.

Changing `Engine.Instance.Configuration.LoggingMode` after initialization does not itself reconfigure the already-running logging pipeline. Use the runtime `EngineLogger` switching methods for a temporary mode change, or place the intended mode in the configuration loaded at startup.

---

## Runtime performance samples

Logging `CPSCalculated` is an easy way to record engine and rendering throughput without writing a per-frame message:

```csharp
Engine.Instance.CPSCalculated += sample =>
{
    string gpuFps = sample.GpuFps is double value
        ? value.ToString("F1")
        : "n/a";

    Log.LogInformation(
        "CPS {GrossCps:F1}; foreground FPS {NetFps:F1}; GPU FPS {GpuFps}",
        sample.GrossCPS,
        sample.NetCPS,
        gpuFps);
};
```

The event runs at the interval specified by `SamplingTimeForCPS`, which defaults to 1.5 seconds. This is generally much more useful than emitting one timing message on every cycle.

Remember to unsubscribe temporary handlers if the containing object can be recreated:

```csharp
private void OnCpsCalculated(
    CyclesPerSecondCalculatedEventArgs sample)
{
    Log.LogDebug("{PerformanceSample}", sample);
}

private void StartDiagnostics()
{
    Engine.Instance.CPSCalculated += OnCpsCalculated;
}

private void StopDiagnostics()
{
    Engine.Instance.CPSCalculated -= OnCpsCalculated;
}
```

---

## Detect logging-pipeline failures

In asynchronous mode, `EngineLogger.LoggingError` is raised when the background worker encounters an exception while writing a record.

```csharp
using System.Diagnostics;

EngineLogger.LoggingError += (_, args) =>
{
    Debug.WriteLine(
        $"Logging failed for {args.CategoryName}: " +
        args.Exception.Message);
};
```

Do not send the failure straight back through `EngineLogger`; the same provider could fail again and create a loop.

This event is a logging-health hook, not a dropped-message counter. It also should not be treated as a general wrapper around exceptions produced by game code.

---

## Desktop and browser behavior

With Gondwana's built-in logger factory:

- desktop platforms use the Debug and Console providers
- browser/WASM platforms use the Debug provider without the Console provider

The browser avoids the console logger because its background-thread behavior is unsuitable for the WASM runtime. As a result, do not assume that logs will appear through identical channels on WinForms, Avalonia, and Blazor.

Use the same `ILogger` calls in game code and let the active platform determine where the built-in provider writes them.

---

## Shutdown and flushing

When the engine is disposed while asynchronous logging is active, it calls `EngineLogger.StopAsyncLogging` using the `FlushAsyncLogsOnShutdown` configuration setting.

Dispose the game host normally so Gondwana has an opportunity to stop the engine and perform that cleanup:

```csharp
protected override void OnFormClosed(FormClosedEventArgs e)
{
    _gameHost?.Dispose();
    _gameHost = null;

    base.OnFormClosed(e);
}
```

The asynchronous flush is best effort. Do not depend on a last-second log record as the only durable copy of important game data. Save data through the persistence system; log that the save succeeded or failed.

---

## Practical logging patterns

### Log state transitions, not continuous state

Prefer:

```csharp
Log.LogDebug(
    "Player state changed from {OldState} to {NewState}",
    oldState,
    newState);
```

over logging the same current state on every cycle.

### Aggregate repetitive events

Instead of logging every collision candidate or particle, count them and periodically report a summary:

```csharp
Log.LogDebug(
    "Resolved {CollisionCount} collisions during the last sample",
    collisionCount);
```

### Include the identity needed to reproduce the problem

Useful fields commonly include:

- scene or level name
- layer identifier
- sprite or entity identifier
- frame or animation name
- world and grid positions
- collision type or profile
- elapsed duration
- platform or backbuffer type

Include the fields that distinguish one occurrence from another. Dumping every available property generally makes the useful evidence harder to find.

### Separate player-facing errors from diagnostic logs

A log entry does not replace an appropriate in-game message or error dialog. Show the player what they need to act; log the technical context a developer needs to diagnose it.

---

## Troubleshooting

| Symptom | Likely explanation or next check |
| --- | --- |
| Only warnings and errors appear | The game host defaults to `LogLevel.Warning`; pass a lower level to `Initialize` |
| A typed logger ignores a new level | It was retained from before `SetLogLevel`; retrieve it again from `EngineLogger` |
| Messages appear late | Asynchronous logging is enabled, so a background worker writes them |
| Messages are missing only during enormous bursts | The bounded asynchronous queue may be saturated and dropping new records |
| Message order does not match breakpoint order | Temporarily use `SwitchToSyncAndFlush()` for deterministic investigation |
| Enabling verbose logs makes the game stutter | Logging volume or provider work has become part of the measured workload |
| `LoggingError` does not report dropped records | Expected; it reports asynchronous write failures, not queue saturation |
| Logs differ between desktop and Blazor | The browser uses a different built-in provider configuration |
| Final messages are sometimes absent | Ensure the host is disposed; remember that shutdown flushing is best effort |
| Changing `Configuration.LoggingMode` at runtime has no effect | Use `EngineLogger` runtime switching methods or configure the mode before initialization |

---

## Recommended defaults

For most Gondwana games:

- initialize development builds at `Debug` or `Information`
- initialize release builds at `Warning`
- keep asynchronous logging enabled
- retain the default queue capacity unless real measurements justify changing it
- keep `FlushAsyncLogsOnShutdown` enabled
- use typed loggers and structured message templates
- aggregate high-frequency evidence
- switch to synchronous logging only for a focused diagnostic session

That approach keeps useful evidence available without turning the log into a second game loop.

---

## Where to read next

- [Debugging and Instrumentation](https://github.com/Isthimius/Gondwana/wiki/Debugging-and-Instrumentation)
- [Engine Configuration](https://github.com/Isthimius/Gondwana/wiki/Engine-Configuration)
- [Configuration Settings](https://github.com/Isthimius/Gondwana/wiki/Configuration-Settings)
- [Gondwana Engine Lifecycle](https://github.com/Isthimius/Gondwana/wiki/Gondwana-Engine-Lifecycle)

Relevant source files:

- [`Gondwana/Logging/EngineLogger.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineLogger_8cs_source.html)
- [`Gondwana/Logging/ModeLogger.cs`](https://isthimius.github.io/Gondwana/api/latest/ModeLogger_8cs_source.html)
- [`Gondwana/Logging/LogEvent.cs`](https://isthimius.github.io/Gondwana/api/latest/LogEvent_8cs_source.html)
- [`Gondwana/Logging/EngineLoggingMode.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineLoggingMode_8cs_source.html)
- [`Gondwana/Configuration/EngineConfiguration.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineConfiguration_8cs_source.html)
- [`Gondwana/Engine.cs`](https://isthimius.github.io/Gondwana/api/latest/Engine_8cs_source.html)
- [`Gondwana.Hosting/GameHostBase.cs`](https://isthimius.github.io/Gondwana/api/latest/GameHostBase_8cs_source.html)
