---
title: "feat: Live asset hot-reload via FileSystemWatcher in Gondwana.Hosting"
---
## Summary
FlatRedBall's "Live Edit" lets developers change assets while the game is running and see changes instantly. This is a significant iteration-speed improvement. This issue tracks adding a `HotReloadWatcher` to `Gondwana.Hosting` that automatically re-imports changed asset files.

## Scope of Work

### `Gondwana.Hosting.HotReloadWatcher`
```csharp
public class HotReloadWatcher : IDisposable
{
    public HotReloadWatcher(IEngineDispatcher dispatcher, string assetRoot);

    // Register a reload handler for a file extension
    public void Register(string extension, Action<string> reloadAction);

    public void Start();
    public void Stop();
}
```

- Uses `System.IO.FileSystemWatcher` internally
- Debounces file-change events (default 100 ms) to avoid partial-write races
- Dispatches reload actions via `IEngineDispatcher.InvokeOnCycle` so all reloads happen on the engine cycle thread (thread-safe)
- Logs each reload to the engine's diagnostic output

### Built-in Handlers
| Extension | Handler |
|---|---|
| `.gondwana-tilesheet` | `TilesheetRegistry.Reload(path)` — reloads image + metadata |
| `.wav`, `.ogg`, `.mp3` | `AudioResourceManager.Reload(path)` |
| `.gondwana-animation` | Animation cache invalidation |

### `GameHostBase` Integration
```csharp
// In game host setup:
host.EnableHotReload(assetRoot: "Assets/");
```
`EnableHotReload` is a no-op on platforms that don't support `FileSystemWatcher` (WASM).

## Acceptance Criteria
- [ ] Overwriting a tilesheet PNG on disk causes the engine to re-render using the new image within 500 ms
- [ ] No crash or visual artefact during the reload transition
- [ ] Hot-reload is a no-op (silent) on WASM / platforms without filesystem watch support
- [ ] No measurable frame-rate impact when no files are being changed

## Key Files / References
- `Gondwana.Hosting/GameHostBase.cs`
- `Gondwana/Drawing/Tilesheets/TilesheetRegistry.cs`
- `Gondwana/Assets/Audio/AudioResourceManager.cs`
- `Gondwana/EngineDispatcher.cs`
- `Gondwana/IEngineDispatcher.cs`
