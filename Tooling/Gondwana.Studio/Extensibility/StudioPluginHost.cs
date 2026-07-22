using Avalonia.Controls;
using CoreHost = Gondwana.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Studio.Extensibility;

/// <summary>
/// Avalonia-specific plugin host. Extends the framework-neutral <see cref="CoreHost"/>
/// by adding methods that retrieve Avalonia UI contributions from loaded plugins.
/// </summary>
public sealed class StudioPluginHost : CoreHost
{
    /// <summary>
    /// StudioPluginHost.
    /// </summary>
    /// <param name="log">log.</param>
    public StudioPluginHost(Action<string> log) : base(log)
    {
    }

    /// <summary>
    /// GetPluginPanels.
    /// </summary>
    /// <returns>The result.</returns>
    public IEnumerable<(string pluginName, Control panel)> GetPluginPanels()
    {
        var panels = new List<(string pluginName, Control panel)>();
        foreach (var plugin in GetPluginsAs<IStudioPlugin>())
        {
            try
            {
                var panel = plugin.CreatePanel();
                if (panel is not null)
                    panels.Add((plugin.Name, panel));
            }
            catch (Exception ex)
            {
                Log($"[Plugin] CreatePanel threw for '{plugin.Name}': {ex.Message}");
            }
        }

        return panels;
    }

    /// <summary>
    /// GetPluginMenuItems.
    /// </summary>
    /// <returns>The result.</returns>
    public IEnumerable<MenuItem> GetPluginMenuItems()
    {
        var items = new List<MenuItem>();
        foreach (var plugin in GetPluginsAs<IStudioPlugin>())
        {
            try
            {
                var menu = plugin.CreateMenuItem();
                if (menu is not null)
                    items.Add(menu);
            }
            catch (Exception ex)
            {
                Log($"[Plugin] CreateMenuItem threw for '{plugin.Name}': {ex.Message}");
            }
        }

        return items;
    }

    private void Log(string message)
    {
        // Surface errors via the Plugins list; nothing else to do here.
        // In a future revision this can propagate to OutputViewModel.
        System.Diagnostics.Debug.WriteLine(message);
    }
}
