using CoreHost = Gondwana.Tooling.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Tooling.Studio.WinForms.Extensibility;

/// <summary>
/// WinForms studio plugin host. Extends the framework-neutral <see cref="CoreHost"/>
/// by adding methods that retrieve WinForms UI contributions from loaded plugins.
/// </summary>
public sealed class StudioPluginHost : CoreHost
{
    /// <summary>Creates a host that shares both Core and WinForms plugin contracts with discovered assemblies.</summary>
    /// <param name="log">Callback receiving discovery, lifecycle, and UI-contribution diagnostics.</param>
    public StudioPluginHost(Action<string> log) : base(log, typeof(IStudioPlugin).Assembly)
    {
    }

    /// <summary>Calls each enabled WinForms plugin to obtain its panel, logging and skipping failed or null contributions.</summary>
    /// <returns>The contributed controls paired with their plugin display names, for the caller to host and dispose.</returns>
    /// <remarks>Call on the UI thread. Contributions are requested on each call; a contribution failure does not disable the plugin.</remarks>
    public IEnumerable<(string pluginName, Control panel)> GetPluginPanels()
        => GetPersistentPluginPanels().Select(entry => (entry.Name, entry.Panel));

    internal IEnumerable<(string Id, string Name, Control Panel)> GetPersistentPluginPanels()
    {
        var result = new List<(string, string, Control)>();

        foreach (var plugin in GetPluginsAs<IStudioPlugin>())
        {
            try
            {
                var panel = plugin.CreatePanel();
                if (panel is not null)
                    result.Add(($"plugin.{plugin.GetType().Assembly.GetName().Name}.{plugin.GetType().FullName}.{plugin.Name}", plugin.Name, panel));
            }
            catch (Exception ex)
            {
                Log($"[Plugin] CreatePanel threw for '{plugin.Name}': {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>Calls each enabled WinForms plugin to obtain its menu item, logging and skipping failed or null contributions.</summary>
    /// <returns>The contributed menu items for the caller to insert and dispose.</returns>
    /// <remarks>Call on the UI thread. Contributions are requested on each call; a contribution failure does not disable the plugin.</remarks>
    public IEnumerable<ToolStripMenuItem> GetPluginMenuItems()
    {
        var result = new List<ToolStripMenuItem>();

        foreach (var plugin in GetPluginsAs<IStudioPlugin>())
        {
            try
            {
                var item = plugin.CreateMenuItem();
                if (item is not null)
                    result.Add(item);
            }
            catch (Exception ex)
            {
                Log($"[Plugin] CreateMenuItem threw for '{plugin.Name}': {ex.Message}");
            }
        }

        return result;
    }
}
