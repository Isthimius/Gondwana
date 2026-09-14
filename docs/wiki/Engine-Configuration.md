Gondwana is code-first, but not every engine setting needs to be hard-coded.

`EngineConfiguration` contains the shared settings that control the engine loop, GPU presentation, input-event throttling, diagnostics, logging, startup state files, and optional game-specific configuration sections.

The most important thing to understand is that Gondwana loads configuration **during engine initialization**. That makes initialization order part of the configuration contract.

---

## Contents

- [The three configuration pieces](#the-three-configuration-pieces)
- [Loading configuration](#loading-configuration)
- [The JSON file](#the-json-file)
- [Initialization order](#initialization-order)
- [Using `GameHostBase`](#using-gamehostbase)
- [Using `Engine` directly](#using-engine-directly)
- [Built-in settings](#built-in-settings)
- [Custom configuration sections](#custom-configuration-sections)
- [Loading state files at startup](#loading-state-files-at-startup)
- [Creating and saving a configuration file](#creating-and-saving-a-configuration-file)
- [Changing settings at runtime](#changing-settings-at-runtime)
- [Configuration vs. engine state](#configuration-vs-engine-state)
- [Threading](#threading)
- [Common mistakes](#common-mistakes)
- [Practical recommendations](#practical-recommendations)
- [Final mental model](#final-mental-model)

---

## The three configuration pieces

Gondwana configuration is built around three related types:

| Type | Purpose |
| --- | --- |
| `EngineConfiguration` | The actual collection of engine and application settings. |
| `EngineConfigurationFile` | Loads and saves an `EngineConfiguration` as JSON. |
| `StateFileMount` | Describes an `EngineState` file that should be merged during startup. |

At runtime, the active configuration is available from the engine singleton:

```csharp
EngineConfiguration config = Engine.Instance.Configuration;
```

`Engine.Configuration` is not the configuration file. It is the in-memory `EngineConfiguration` object that the engine is currently using.

---

## Loading configuration

Configuration is loaded by `Engine.Initialize`:

```csharp
Engine.Instance.Initialize("gondwana.json");
```

If no path is supplied, Gondwana looks for the default file name:

```text
gondwana.json
```

A relative path is resolved from the application's current working directory. For a deployed game, passing an explicit path is usually clearer than relying on whichever directory happened to launch the process.

If the file does not exist, Gondwana does not fail initialization. `EngineConfigurationFile.Load` returns a new `EngineConfiguration` containing its built-in defaults.

That fallback does **not** by itself create a file on disk.

---

## The JSON file

The outer JSON property must be named `EngineConfig`, because that is the section read by `EngineConfigurationFile.Load`.

```json
{
  "EngineConfig": {
    "TargetFPS": 60,
    "VSync": true,
    "MsaaSampleCount": 1,
    "SamplingTimeForCPS": 1.5,
    "TimeBetweenKeyboardEvents": 0.03,
    "TimeBetweenGamepadEvents": 0.03,
    "TimeBetweenMouseEvents": 0.03,
    "TimeBetweenTouchEvents": 0.03,
    "LoggingMode": "Asynchronous",
    "LoggingQueueCapacity": 8192,
    "FlushAsyncLogsOnShutdown": true,
    "ConfigurationSections": {
      "audio": {
        "masterVolume": "0.8"
      },
      "gameplay": {
        "difficulty": "normal"
      }
    }
  }
}
```

You may omit settings that should retain their defaults. The file does not need to repeat every property.

Configuration property names are case-insensitive when loaded through the .NET configuration binder, but using the C# property names exactly makes the file easier to compare with the API.

---

## Initialization order

`Engine.Instance` exists before the engine is initialized, and it initially exposes a default `EngineConfiguration`. During `Initialize`, Gondwana loads the selected file and **replaces that object** with the loaded configuration.

The simplified sequence is:

1. Raise `PreInitialization`.
2. Load the configuration file, or create default settings if it is missing.
3. Replace `Engine.Configuration` with the loaded `EngineConfiguration`.
4. Apply `LoggingMode` and `LoggingQueueCapacity` to `EngineLogger`.
5. Merge configured `StateFiles` in list order.
6. Initialize the supplied input adapters.
7. Raise `PostInitialization` and `InitializationComplete`.
8. Start the engine loop when requested by the caller or host.

This means the following is unreliable:

```csharp
// Do not use this as an initialization-time override.
Engine.Instance.Configuration.TargetFPS = 120;

// Initialize replaces Configuration with a newly loaded object.
Engine.Instance.Initialize("gondwana.json");
```

After `Initialize`, `TargetFPS` will come from the file—or from the built-in default—not necessarily from the earlier assignment.

The safe choices are:

- put the value in the JSON file; or
- assign it after `Initialize` has completed.

---

## Using `GameHostBase`

The hosting package passes the configuration path into engine initialization:

```csharp
gameHost.Initialize(
    configPath: "gondwana.json",
    autoSaveConfig: true);
```

With `autoSaveConfig: true`, the engine retains the configuration-file wrapper and saves changes when the host disposes the engine cleanly. Leave it `false` or omit it when the startup file should remain read-only.

If a host subclass needs a deliberate code-level override, `OnEngineInitialized` is the clean hook. It runs after the file has been loaded and before the engine is started.

```csharp
protected override void OnEngineInitialized()
{
    base.OnEngineInitialized();

    Engine.Configuration.TargetFPS = 120;
}
```

Use this for values that genuinely belong to the host or build. Prefer the JSON file for values that users, testers, or deployment environments may need to change without recompiling the game.

One host-specific detail is worth knowing: `GameHostBase.Initialize` configures input, creates game content, and builds the initial scene before it calls `Engine.Initialize`. The file-backed engine configuration is therefore not final inside hooks such as `ConfigureKeyboard`, `ConfigureMouse`, `LoadAssets`, `CreateInitialScene`, or `CreateSprites`.

If content creation depends on a configuration value, either:

- load that value independently before host initialization;
- move the dependent work to a post-engine-initialization hook; or
- adjust the host lifecycle for the application's needs.

---

## Using `Engine` directly

Applications that do not use `GameHostBase` can initialize the engine themselves:

```csharp
var engine = Engine.Instance;

engine.Initialize("gondwana.json");

// Optional code-level overrides now survive initialization.
engine.Configuration.TargetFPS = 120;

engine.Start();
```

`Start()` automatically calls `Initialize()` only if the engine has not already been initialized. That convenience uses the default configuration path, so call `Initialize(customPath)` yourself when a specific file is required.

`Start()` must run on a thread with a valid `SynchronizationContext`, normally the platform UI thread.

---

## Built-in settings

### Rendering and presentation

| Setting | Default | Behavior |
| --- | ---: | --- |
| `TargetFPS` | `60` | Limits foreground rendering. `0` means no upper limit. Negative values are clamped to `0`. |
| `VSync` | `true` | Enables vertical synchronization on GPU backbuffers. CPU bitmap backbuffers ignore it. |
| `MsaaSampleCount` | `1` | Selects GPU multisample anti-aliasing. `1` disables MSAA; values below `1` are clamped to `1`. |

`TargetFPS` affects both rendering paths. The engine loop reads it when deciding whether foreground rendering is due, and its setter also updates registered `GpuBackbuffer` instances.

`VSync` is GPU-only. Changes are propagated to registered GPU backbuffers and applied lazily during a later paint callback.

`MsaaSampleCount` is also GPU-only. Changing it updates the requested value, but an existing GPU render target must be recreated before the new sample count takes effect. A resize or another call to `GpuBackbuffer.Initialize` can cause that recreation. Unsupported sample counts fall back to `1`.

### Performance sampling

| Setting | Default | Behavior |
| --- | ---: | --- |
| `SamplingTimeForCPS` | `1.5` seconds | Controls how often Gondwana calculates and reports engine-cycle and frame-rate measurements. `0` disables sampling. |

Gondwana converts this value to high-resolution timer ticks through the read-only `SamplingTimeForCPSTicks` property.

This is a diagnostic sampling interval, not the update timestep and not the target frame rate.

### Input-event throttling

| Setting | Default | Used by |
| --- | ---: | --- |
| `TimeBetweenKeyboardEvents` | `0.03` seconds | Repeated keyboard events. |
| `TimeBetweenGamepadEvents` | `0.03` seconds | Repeated gamepad-button events. |
| `TimeBetweenMouseEvents` | `0.03` seconds | Mouse monitoring and high-frequency pointer activity. |
| `TimeBetweenTouchEvents` | `0.03` seconds | Touch-movement events. Touch begin/end transitions are not throttled. |

These values are defaults used when monitoring begins and no explicit interval is supplied. They are copied into the corresponding input-event configuration.

Changing an engine-level input interval later does not retroactively rewrite monitors that already exist. Reconfigure or restart the relevant monitor if its effective interval must change.

An explicit per-monitor value overrides the engine default:

```csharp
Engine.Input.KeyboardEventPoller?.StartMonitoringKey(
    keyCode: 32,
    displayName: "Space",
    timeBetweenEvents: 0.10);
```

### Logging

| Setting | Default | Behavior |
| --- | ---: | --- |
| `LoggingMode` | `Asynchronous` | Chooses background queued logging or immediate synchronous logging. |
| `LoggingQueueCapacity` | `8192` | Maximum size of the asynchronous logging queue. Values must be greater than zero. |
| `FlushAsyncLogsOnShutdown` | `true` | Drains queued asynchronous log entries when the engine is disposed. When `false`, logging still stops, but shutdown does not wait for the queue to drain. |

Asynchronous logging protects the engine loop from slow log destinations, but it is intentionally fire-and-forget: when the bounded queue is full, new entries may be dropped.

Synchronous logging preserves immediate ordering and delivery more predictably, but logging work occurs on the calling thread and can hurt frame timing if used heavily.

`Engine.Initialize` applies both `LoggingMode` and `LoggingQueueCapacity`. If asynchronous logging was already started by host or pre-initialization work, Gondwana recreates the asynchronous pipeline so its channel uses the configured capacity.

Changing only `Engine.Configuration.LoggingMode` or `LoggingQueueCapacity` after initialization does not rebuild the active logging pipeline. For a runtime change, update the logger explicitly with `EngineLogger.SwitchToSyncAndFlush()` or `EngineLogger.SwitchToAsync(capacity)` as appropriate.

---

## Custom configuration sections

`ConfigurationSections` lets a game store stable application settings without adding a new property to Gondwana's core `EngineConfiguration` class.

It is a dictionary of named sections, each containing string keys and string values:

```csharp
Engine.Configuration.SetConfigurationValue(
    section: "audio",
    key: "masterVolume",
    value: "0.8");

string difficulty = Engine.Configuration.GetConfigurationValue(
    section: "gameplay",
    key: "difficulty",
    defaultValue: "normal")!;
```

The two-argument indexer provides a shorter form:

```csharp
Engine.Configuration["video", "fullscreen"] = "true";

string? fullscreen =
    Engine.Configuration["video", "fullscreen"];
```

Available helpers include:

- `HasConfigurationSection`
- `CreateConfigurationSection`
- `GetConfigurationSection`
- `RemoveConfigurationSection`
- `ClearConfigurationSection`
- `ClearConfigurationSections`
- `HasConfigurationValue`
- `GetConfigurationValue`
- `SetConfigurationValue`
- `RemoveConfigurationValue`

Values are deliberately stored as strings. Parse them at the boundary where the game consumes them:

```csharp
string? rawVolume =
    Engine.Configuration["audio", "masterVolume"];

double volume = double.TryParse(rawVolume, out double parsed)
    ? parsed
    : 1.0;
```

Use configuration sections for settings such as:

- audio levels
- difficulty selection
- feature flags
- graphics preferences
- environment or content identifiers

Do not use them as a save-game system. Player position, inventory, current scene state, and other frequently changing runtime data belong in game state or `EngineState`.

---

## Loading state files at startup

`EngineConfiguration.StateFiles` can declare one or more serialized `EngineState` files to merge during initialization.

Each `StateFileMount` supplies:

| Property | Purpose |
| --- | --- |
| `File` | Path to the state file. |
| `IsCompressed` | Whether the file uses GZip compression. |
| `OverwriteExisting` | Whether incoming values replace existing values during a conflict. |
| `EngineStateParts` | Flags selecting which portions of the saved state should be merged. |

The equivalent file-building code looks like this:

```csharp
using var configFile =
    EngineConfigurationFile.CreateNew("gondwana.json");

configFile.EngineConfig.StateFiles =
[
    new StateFileMount
    {
        File = "Content/base-state.json.gz",
        IsCompressed = true,
        OverwriteExisting = false,
        EngineStateParts =
            EngineStateParts.AssetsFiles |
            EngineStateParts.Tilesheets |
            EngineStateParts.Cycles
    }
];

configFile.Save();
```

Configured state files are merged in list order. Later files therefore see whatever earlier files already contributed, and `OverwriteExisting` determines which side wins when data conflicts.

State-file mounts are processed only during initialization. Adding an entry to `StateFiles` after startup does not load it automatically; call the appropriate `EngineState` load or merge API yourself.

---

## Creating and saving a configuration file

Use `EngineConfigurationFile.CreateNew` to build a file from Gondwana's defaults:

```csharp
using var configFile =
    EngineConfigurationFile.CreateNew("gondwana.json");

configFile.EngineConfig.TargetFPS = 60;
configFile.EngineConfig.VSync = true;
configFile.EngineConfig["audio", "masterVolume"] = "0.8";

configFile.Save();
```

To edit an existing file:

```csharp
using var configFile =
    EngineConfigurationFile.Load("gondwana.json");

configFile.EngineConfig.TargetFPS = 120;
configFile.Save();
```

You can also request save-on-dispose when directly owning the wrapper:

```csharp
using var configFile =
    EngineConfigurationFile.Load(
        "gondwana.json",
        autoSave: true);

configFile.EngineConfig.TargetFPS = 120;
// Saved when configFile is disposed.
```

`Save()` writes indented JSON. `Save(path)` writes to a new location and updates the wrapper's `FilePath`.

Treat `EngineConfigurationFile` as the persistence wrapper and `EngineConfiguration` as the settings object. Keeping that distinction straight makes the API much less mysterious.

The engine can own that persistence lifecycle for you:

```csharp
Engine.Instance.Initialize(
    configFileName: "gondwana.json",
    autoSaveConfig: true);

Engine.Instance.Configuration.TargetFPS = 120;

// Dispose performs a clean shutdown and saves the configuration.
Engine.Instance.Dispose();
```

`GameHostBase.Initialize` exposes the same option through its `autoSaveConfig` parameter. Disposing the host disposes the engine, which in turn disposes and saves the retained `EngineConfigurationFile`.

---

## Changing settings at runtime

Some configuration properties are read continuously or propagate changes. Others are consumed only at initialization or when a subsystem is configured.

| Setting group | Runtime behavior |
| --- | --- |
| `TargetFPS` | Takes effect without restarting and updates registered GPU backbuffers. |
| `VSync` | Propagates to GPU backbuffers; applied on a later GPU paint. |
| `MsaaSampleCount` | Stores and propagates the request; takes effect after GPU render-target recreation. |
| `SamplingTimeForCPS` | Read by the running engine's sampling logic. |
| Input intervals | Used as defaults when monitors are created or reconfigured. Existing monitor settings remain unchanged. |
| Logging settings | Mode and queue capacity are applied during initialization. Later configuration-only assignments do not rebuild the active logger. |
| `StateFiles` | Processed only during initialization. |
| `ConfigurationSections` | Available immediately to game code; Gondwana does not assign application-specific meaning to them. |

Runtime changes modify the in-memory object immediately. They are written back to the original JSON file during clean engine disposal when `autoSaveConfig` was enabled at initialization. With autosave disabled, persist intended changes explicitly.

Similarly, treat the configuration file as a startup snapshot. The current engine does not replace or rebind `Engine.Configuration` merely because the JSON file changes while the game is running.

---

## Configuration vs. engine state

These concepts overlap just enough to be confused, but they serve different jobs.

| Use | `EngineConfiguration` | `EngineState` |
| --- | --- | --- |
| Engine timing and presentation settings | Yes | No |
| Stable user or application preferences | Yes, through `ConfigurationSections` | Usually no |
| Startup declarations for state files | Yes | No |
| Assets, tilesheets, cycles, scenes, sprites, and audio snapshots | No | Yes |
| Frequently changing gameplay data | No | Potentially, or use a game-specific save model |
| Loaded during engine initialization | Yes | Yes, when declared by `StateFiles` |

A useful rule is:

> Configuration describes how the application should start and operate. State describes what the application currently contains.

---

## Threading

`Engine.Configuration` uses volatile reads and writes for the configuration **reference**, so replacing or retrieving that reference is thread-safe inside the engine.

That does not make every mutable property and nested dictionary automatically safe for concurrent mutation.

In particular, `ConfigurationSections` is backed by ordinary `Dictionary` instances. Configure it before the engine starts, or marshal runtime mutations to the engine thread when other code may read it concurrently:

```csharp
Engine.Instance.EngineDispatcher.Post(() =>
{
    Engine.Instance.Configuration["gameplay", "difficulty"] = "hard";
});
```

Reading stable values from other threads is common. Simultaneous reads and writes to the same mutable collection require coordination.

---

## Common mistakes

### A value is set, then mysteriously returns to its default

The value was probably assigned before `Engine.Initialize`. Initialization replaced the default configuration object with the file-backed object.

Put the value in JSON or assign it after initialization.

### The JSON file exists, but none of its values load

Check the outer property name. It must be:

```json
"EngineConfig"
```

Also verify the path relative to the process's current working directory.

### Editing the JSON file does nothing while the game is running

Treat the file as an initialization source. The active `Engine.Configuration` object is not automatically rebound to every disk edit.

### Changing an input interval does not affect an existing monitor

The engine setting is a default copied when monitoring is configured. Restart or reconfigure that monitor.

### `VSync` or MSAA does nothing on a bitmap backbuffer

Both settings are GPU-specific. `BitmapBackbuffer` ignores them.

### Changing `MsaaSampleCount` does not alter the current frame

The GPU render target must be recreated before the new sample count can take effect.

### A runtime change is gone after restarting

Enable `autoSaveConfig` when initializing the engine or host, and dispose it cleanly. If autosave is intentionally disabled, save the intended value through an owned `EngineConfigurationFile` or another deliberate persistence path.

---

## Practical recommendations

- Keep `gondwana.json` small. Omit values that can safely use engine defaults.
- Use an explicit configuration path in deployed applications.
- Put environment- or user-tunable values in the file.
- Apply deliberate build-specific overrides in `OnEngineInitialized`.
- Enable `autoSaveConfig` only when runtime changes should be written back to the startup file.
- Use `ConfigurationSections` for stable game settings, not active gameplay state.
- Configure input defaults before registering keys, buttons, mouse monitoring, or touch monitoring.
- Mutate nested configuration dictionaries on one known thread.
- Use `EngineState` for engine content snapshots and save data, not as a replacement for ordinary startup configuration.

---

## Final mental model

> `EngineConfigurationFile` is the JSON persistence wrapper.

> `EngineConfiguration` is the live settings object.

> `Engine.Instance.Configuration` becomes final only when initialization loads or creates it.

> File values belong before initialization; code overrides belong after initialization.

> With autosave enabled, clean disposal writes the live configuration back to its startup file.

> Some settings are live, some are copied into subsystems, and some are startup-only.

Once those distinctions are clear, Gondwana configuration is straightforward: load a small startup document, let the engine establish its shared settings, and keep transient game state somewhere designed to hold state.

---

## Source files

- [`Gondwana/Configuration/EngineConfiguration.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Configuration/EngineConfiguration.cs)
- [`Gondwana/Configuration/EngineConfigurationFile.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Configuration/EngineConfigurationFile.cs)
- [`Gondwana/Configuration/StateFileMount.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Configuration/StateFileMount.cs)
- [`Gondwana/Engine.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana/Engine.cs)
- [`Gondwana.Hosting/GameHostBase.cs`](https://github.com/Isthimius/Gondwana/blob/master/Gondwana.Hosting/GameHostBase.cs)

## Related wiki pages

- [[Serialization and EngineState]]
- [[Assets Files]]
- [[Backbuffers]]
- [[GL Rendering Path]]
- [[Input Handling]]
