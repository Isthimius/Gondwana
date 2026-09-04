using Gondwana.Rendering;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Extensibility;

/// <summary>
/// EnginePluginRegistry.
/// </summary>
public static class EnginePluginRegistry
{
    private static readonly object _lock = new();
    private static readonly List<IEnginePlugin> _plugins = [];
    private static readonly HashSet<IEnginePlugin> _disabledPlugins = [];
    private static IEnginePlugin[] _snapshot = [];

    /// <summary>
    /// Gets a thread-safe snapshot of all currently registered plugins.
    /// </summary>
    public static IReadOnlyList<IEnginePlugin> All
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    /// <summary>
    /// Register.
    /// </summary>
    /// <param name="plugin">plugin.</param>
    public static void Register(IEnginePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        lock (_lock)
        {
            if (_plugins.Contains(plugin))
                return;

            _plugins.Add(plugin);
            _disabledPlugins.Remove(plugin);
            _snapshot = [.. _plugins];
        }
    }

    /// <summary>
    /// Unregister.
    /// </summary>
    /// <param name="plugin">plugin.</param>
    public static void Unregister(IEnginePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        lock (_lock)
        {
            _plugins.Remove(plugin);
            _disabledPlugins.Remove(plugin);
            _snapshot = [.. _plugins];
        }
    }

    internal static void InvokeInitialize(Engine engine) =>
        Invoke(engine, p => p.OnInitialize(engine), "OnInitialize");

    internal static void InvokePreCycle(Engine engine, double deltaSeconds) =>
        Invoke(engine, p => p.OnPreCycle(engine, deltaSeconds), "OnPreCycle");

    internal static void InvokePreFrameRender(Engine engine, double deltaSeconds) =>
        Invoke(engine, p => p.OnPreFrameRender(engine, deltaSeconds), "OnPreFrameRender");

    internal static void InvokePostFrameRender(Engine engine, double deltaSeconds) =>
        Invoke(engine, p => p.OnPostFrameRender(engine, deltaSeconds), "OnPostFrameRender");

    internal static void InvokePostRenderCanvas(Engine engine, RenderSurfaceHostBase host, SKCanvas canvas) =>
        Invoke(engine, p => p.OnPostRenderCanvas(engine, host, canvas), "OnPostRenderCanvas");

    internal static void InvokePostCycle(Engine engine, double deltaSeconds) =>
        Invoke(engine, p => p.OnPostCycle(engine, deltaSeconds), "OnPostCycle");

    internal static void InvokeShutdown(Engine engine) =>
        Invoke(engine, p => p.OnShutdown(engine), "OnShutdown");

    private static void Invoke(Engine engine, Action<IEnginePlugin> callback, string hook)
    {
        IEnginePlugin[] snapshot;
        lock (_lock)
            snapshot = _snapshot;

        foreach (var plugin in snapshot)
        {
            lock (_lock)
            {
                if (_disabledPlugins.Contains(plugin))
                    continue;
            }

            try
            {
                callback(plugin);
            }
            catch (Exception ex)
            {
                lock (_lock)
                    _disabledPlugins.Add(plugin);

                Engine.Logger.LogError(
                    ex,
                    "Engine plugin '{PluginName}' ({PluginVersion}) threw in {Hook} and was disabled.",
                    plugin.Name,
                    plugin.Version,
                    hook);
            }
        }
    }
}
