This page is a quick reference for every setting currently defined by `EngineConfiguration`.

For the full loading, saving, initialization-order, and autosave lifecycle, see [[Engine Configuration]].

---

## JSON container

Engine settings belong beneath the `EngineConfig` property:

```json
{
  "EngineConfig": {
    "TargetFPS": 60
  }
}
```

Every setting is optional. When a property is omitted, Gondwana uses the default shown below.

---

## Rendering and presentation

| Setting | Type | Default | What it does | When it takes effect |
| --- | --- | ---: | --- | --- |
| `TargetFPS` | `int` | `60` | Sets the maximum foreground render rate. Use `0` for no engine-imposed upper limit. Negative values are clamped to `0`. | Immediately. The value is read by the engine loop and propagated to registered GPU backbuffers. |
| `VSync` | `bool` | `true` | Synchronizes GPU presentation with the monitor to reduce tearing. It can limit the effective frame rate to the display refresh rate. Bitmap backbuffers ignore it. | On the next GPU paint after the change. |
| `MsaaSampleCount` | `int` | `1` | Requests multisample anti-aliasing for GPU rendering. `1` disables MSAA; common enabled values are `2`, `4`, and `8`. Values below `1` are clamped to `1`. | After the GPU render target is recreated, such as during a resize or another backbuffer initialization. |

### `TargetFPS`

`TargetFPS` controls how often Gondwana performs foreground rendering. It does not change the speed of the background engine cycle.

```json
"TargetFPS": 120
```

Setting it to `0` removes Gondwana's frame-rate cap, but GPU presentation may still be limited by `VSync`.

### `VSync`

```json
"VSync": true
```

Use `true` for the usual tear-free presentation. Use `false` when uncapped GPU presentation or latency testing matters more than possible tearing.

This setting applies only to `GpuBackbuffer`.

### `MsaaSampleCount`

```json
"MsaaSampleCount": 4
```

Higher sample counts can smooth polygon and line edges, but consume more GPU memory and rendering time. Hardware support varies. If the requested count is unsupported, the GPU backbuffer falls back to `1` so that it can still create a valid render target.

This setting applies only to `GpuBackbuffer`.

---

## Performance sampling

| Setting | Type | Default | What it does | When it takes effect |
| --- | --- | ---: | --- | --- |
| `SamplingTimeForCPS` | `double` | `1.5` seconds | Sets the interval between Gondwana's cycles-per-second and frame-rate measurements. Use `0` to disable performance sampling. Negative values are clamped to `0`. | Immediately; the running engine reads it during its cycle. |

```json
"SamplingTimeForCPS": 1.5
```

A shorter interval updates the measurements more often but makes them more volatile. A longer interval produces steadier averages but responds more slowly to performance changes.

This is a diagnostics interval. It is not the simulation timestep and does not limit the frame rate.

`SamplingTimeForCPSTicks` is a read-only helper calculated from `SamplingTimeForCPS`. It is marked `JsonIgnore` and is **not** a configuration-file setting.

---

## Input-event throttling

| Setting | Type | Default | What it does |
| --- | --- | ---: | --- |
| `TimeBetweenKeyboardEvents` | `double` | `0.03` seconds | Sets the default minimum delay between repeated events for a monitored key. |
| `TimeBetweenGamepadEvents` | `double` | `0.03` seconds | Sets the default minimum delay between repeated events for a monitored gamepad button. |
| `TimeBetweenMouseEvents` | `double` | `0.03` seconds | Sets the default minimum delay for monitored mouse activity, including high-frequency movement. |
| `TimeBetweenTouchEvents` | `double` | `0.03` seconds | Sets the default minimum delay between touch-movement events. Touch begin and end transitions are not throttled. |

```json
"TimeBetweenKeyboardEvents": 0.03,
"TimeBetweenGamepadEvents": 0.03,
"TimeBetweenMouseEvents": 0.03,
"TimeBetweenTouchEvents": 0.03
```

These values are expressed in seconds. The default `0.03` is 30 milliseconds.

They prevent a held key, held button, mouse movement, or touch drag from flooding the application with events. A value of `0` removes the delay.

Each value is a **default**. It is copied into an input monitor when that monitor is configured without an explicit interval. Changing the engine setting later does not rewrite monitors that already exist; reconfigure or restart the relevant monitor instead.

An interval supplied directly to a monitor takes precedence over the engine default.

---

## Logging

| Setting | Type | Default | What it does | When it takes effect |
| --- | --- | ---: | --- | --- |
| `LoggingMode` | `EngineLoggingMode` | `Asynchronous` | Chooses queued background logging or immediate logging on the calling thread. | Applied by `Engine.Initialize`. A later configuration-only change does not rebuild the active logger. |
| `LoggingQueueCapacity` | `int` | `8192` | Sets the maximum number of entries held by the asynchronous logging queue. New entries may be dropped when the queue is full. Must be greater than `0`. | Applied when asynchronous logging is initialized or explicitly restarted. |
| `FlushAsyncLogsOnShutdown` | `bool` | `true` | Chooses whether engine disposal waits for queued asynchronous log entries to drain before the logger stops. | Read during engine disposal. |

### `LoggingMode`

```json
"LoggingMode": "Asynchronous"
```

Available values are:

| Value | Behavior |
| --- | --- |
| `Asynchronous` | Enqueues log entries and writes them on a background thread. This protects frame timing, but entries can be dropped if the bounded queue fills. |
| `Synchronous` | Writes each entry immediately on the calling thread. Delivery and ordering are more direct, but slow log destinations can stall engine work. |

For normal gameplay, `Asynchronous` is the practical default. `Synchronous` can be useful while diagnosing startup failures or ordering-sensitive logging.

### `LoggingQueueCapacity`

```json
"LoggingQueueCapacity": 8192
```

Increase the capacity if short bursts routinely fill the queue and the additional memory is acceptable. A larger queue absorbs a longer burst; it does not make a slow log destination faster.

### `FlushAsyncLogsOnShutdown`

```json
"FlushAsyncLogsOnShutdown": true
```

With `true`, a clean engine shutdown drains queued entries before stopping the asynchronous logger. With `false`, the logger still stops, but remaining queued entries may be discarded so shutdown can finish sooner.

To change the active logging pipeline after initialization, use the corresponding `EngineLogger` switching method rather than changing only the configuration property.

---

## Startup state files

| Setting | Type | Default | What it does | When it takes effect |
| --- | --- | ---: | --- | --- |
| `StateFiles` | `List<StateFileMount>?` | `null` | Declares serialized `EngineState` files that Gondwana should merge during startup. | Once, during `Engine.Initialize`, in list order. |

```json
"StateFiles": [
  {
    "File": "Content/base-state.json.gz",
    "IsCompressed": true,
    "OverwriteExisting": false,
    "EngineStateParts": "All"
  }
]
```

Each item contains:

| Property | Type | Default | What it does |
| --- | --- | ---: | --- |
| `File` | `string` | `""` | Supplies the path to the saved engine-state file. |
| `IsCompressed` | `bool` | `false` | Tells Gondwana whether the file is GZip-compressed. |
| `OverwriteExisting` | `bool` | `false` | When `true`, incoming values win conflicts. When `false`, existing values are preserved. |
| `EngineStateParts` | `EngineStateParts` flags | `All` | Selects which state groups to merge: `AssetsFiles`, `Tilesheets`, `Cycles`, `Scenes`, `Sprites`, and/or `Audio`. |

Adding a mount after initialization does not load it automatically. Use the appropriate `EngineState` load or merge API for runtime work.

For the state format itself, see [[Serialization and EngineState]].

---

## Game-specific configuration

| Setting | Type | Default | What it does | When it takes effect |
| --- | --- | ---: | --- | --- |
| `ConfigurationSections` | `Dictionary<string, Dictionary<string, string>>` | Empty | Stores game- or application-specific settings as named sections containing string key/value pairs. | Immediately available to game code. Gondwana assigns no built-in behavior to the values. |

```json
"ConfigurationSections": {
  "audio": {
    "masterVolume": "0.8",
    "muted": "false"
  },
  "gameplay": {
    "difficulty": "normal"
  }
}
```

All values are strings by design. Parse them when the game consumes them:

```csharp
string? rawVolume =
    Engine.Instance.Configuration["audio", "masterVolume"];

double volume = double.TryParse(rawVolume, out double parsed)
    ? parsed
    : 1.0;
```

Use configuration sections for stable preferences, feature flags, content identifiers, and similar startup settings. Do not use them for player position, inventory, current-scene state, or other frequently changing gameplay data.

Useful APIs include:

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
- `config[section]`
- `config[section, key]`

---

## Complete example

This example shows every top-level setting. Real files should usually omit values that can use their defaults.

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
    "StateFiles": [
      {
        "File": "Content/base-state.json.gz",
        "IsCompressed": true,
        "OverwriteExisting": false,
        "EngineStateParts": "All"
      }
    ],
    "ConfigurationSections": {
      "audio": {
        "masterVolume": "0.8"
      }
    }
  }
}
```

Hand-written JSON can use enum names such as `"Asynchronous"` and `"All"`. A file produced by `EngineConfigurationFile.Save()` may represent enum values using their underlying integers; both forms load into the same enum properties.

---

## Runtime behavior at a glance

| Behavior | Settings |
| --- | --- |
| Read or propagated while the engine is running | `TargetFPS`, `VSync`, `SamplingTimeForCPS` |
| Requires GPU render-target recreation | `MsaaSampleCount` |
| Copied when an input monitor is configured | `TimeBetweenKeyboardEvents`, `TimeBetweenGamepadEvents`, `TimeBetweenMouseEvents`, `TimeBetweenTouchEvents` |
| Applied to the logger during initialization | `LoggingMode`, `LoggingQueueCapacity` |
| Read during shutdown | `FlushAsyncLogsOnShutdown` |
| Processed only during initialization | `StateFiles` |
| Immediately available to application code | `ConfigurationSections` |

Changing a property changes the live `EngineConfiguration` object. It is written back to the startup file on clean disposal only when configuration autosave was enabled during engine or host initialization.

---

## Source files

- [`Gondwana/Configuration/EngineConfiguration.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineConfiguration_8cs_source.html)
- [`Gondwana/Configuration/StateFileMount.cs`](https://isthimius.github.io/Gondwana/api/latest/StateFileMount_8cs_source.html)
- [`Gondwana/Logging/EngineLoggingMode.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineLoggingMode_8cs_source.html)
- [`Gondwana/EngineStateParts.cs`](https://isthimius.github.io/Gondwana/api/latest/EngineStateParts_8cs_source.html)

## Related wiki pages

- [[Engine Configuration]]
- [[Serialization and EngineState]]
- [[Input Handling]]
- [[Backbuffers]]
- [[GL Rendering Path]]
