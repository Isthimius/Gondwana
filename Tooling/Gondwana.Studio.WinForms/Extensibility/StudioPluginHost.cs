using CoreHost = Gondwana.Studio.Core.Extensibility.StudioPluginHost;

namespace Gondwana.Studio.WinForms.Extensibility;

/// <summary>
/// WinForms studio plugin host. Extends the framework-neutral <see cref="CoreHost"/>
/// by adding methods that retrieve WinForms UI contributions from loaded plugins.
/// </summary>
public sealed class StudioPluginHost : CoreHost
{
    /// <summary>
    /// StudioPluginHost.
    /// </summary>
    /// <param name="log">Logging callback.</param>
    public StudioPluginHost(Action<string> log) : base(log)
    {
    }

    /// <summary>
    /// Returns panels contributed by WinForms-compatible plugins.
    /// </summary>
    public IEnumerable<(string pluginName, Control panel)> GetPluginPanels()
    {
        var result = new List<(string, Control)>();
        foreach (var plugin in GetPluginsAs<IStudioPlugin>())
        {
            try
            {
                var panel = plugin.CreatePanel();
                if (panel is not null)
                    result.Add((plugin.Name, panel));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Plugin] CreatePanel threw for '{plugin.Name}': {ex.Message}");
            }
        }
        return result;
    }

    /// <summary>
    /// Returns menu items contributed by WinForms-compatible plugins.
    /// </summary>
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
                System.Diagnostics.Debug.WriteLine($"[Plugin] CreateMenuItem threw for '{plugin.Name}': {ex.Message}");
            }
        }
        return result;
    }
}
