---
title: "feat: IEnginePlugin and IStudioPlugin extension system"
---
## Summary
GameMaker 2024 added first-class plugin support for both runtime and IDE. Gondwana has a `Gondwana/Extensibility/` namespace that is currently empty. This issue formalises a plugin/extension API for both the engine and Gondwana.Studio.

## Scope of Work

### Engine Plugin (`Gondwana/Extensibility/`)

```csharp
public interface IEnginePlugin
{
    string Name    { get; }
    string Version { get; }

    void OnInitialize(Engine engine);
    void OnPreCycle(Engine engine, double deltaMs);
    void OnPostCycle(Engine engine, double deltaMs);
    void OnShutdown(Engine engine);
}
```

**`EnginePluginRegistry`**
```csharp
public static class EnginePluginRegistry
{
    public static void Register(IEnginePlugin plugin);
    public static void Unregister(IEnginePlugin plugin);
    public static IReadOnlyList<IEnginePlugin> All { get; }
}
```

- `Engine` calls registered plugins at each hook point in the cycle
- Plugin exceptions are caught, logged via the engine's diagnostic output, and do not crash the engine
- Order of invocation matches registration order

### Studio Plugin (`Tooling/Gondwana.Studio/Extensibility/`)

```csharp
public interface IStudioPlugin
{
    string Name { get; }
    Control?  CreatePanel();      // returns a dockable Avalonia panel, or null
    MenuItem? CreateMenuItem();   // returns a menu item entry, or null
    void OnProjectOpened(string projectPath);
    void OnProjectClosed();
}
```

- Discovered at startup by scanning assemblies in a `plugins/` directory next to `Gondwana.Studio.exe`
- Uses `System.Runtime.Loader.AssemblyLoadContext` for isolation (plugin crash does not bring down Studio)
- Plugin load errors are surfaced in the Studio output/log pane

## Acceptance Criteria
- [ ] A test `IEnginePlugin` that logs cycle times can be registered and runs without modifying engine source
- [ ] A Gondwana.Studio plugin that adds a custom panel docks correctly and survives project open/close
- [ ] A plugin that throws in `OnPreCycle` or `OnProjectOpened` is caught, logged, and disabled — it does not crash the host
- [ ] Studio plugin discovery from the `plugins/` folder works on Windows, macOS, and Linux

## Key Files / References
- `Gondwana/Extensibility/` (currently empty — this issue fills it)
- `Gondwana/Engine.cs` (hook points in cycle loop)
- `Tooling/Gondwana.Studio/`
- GameMaker 2024 plugin API: https://manual.gamemaker.io/monthly/en/#t=IDE_Tools%2FPackages.htm
