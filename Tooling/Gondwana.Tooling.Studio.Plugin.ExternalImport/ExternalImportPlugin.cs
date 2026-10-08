using Gondwana.Tooling.Studio.Core.Extensibility;
using IStudioPlugin = Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.Plugin.ExternalImport;

/// <summary>
/// Represents external import plugin.
/// </summary>
public sealed class ExternalImportPlugin : IStudioPlugin, IStudioPluginHostServicesAware
{
    private ExternalImportPanel? panel;
    private IStudioPluginHostServices? services;
    private string? directory;
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => "External Asset Importer";
    /// <summary>
    /// Attaches the Studio services used by this plugin.
    /// </summary>
    /// <param name="hostServices">The host services.</param>
    public void AttachHostServices(IStudioPluginHostServices hostServices)
    {
        services = hostServices;
        if (panel is not null) panel.HostServices = services;
    }
    /// <summary>
    /// Creates the plugin's dockable user interface.
    /// </summary>
    /// <returns>The resulting control.</returns>
    public Control CreatePanel() => panel ??= new ExternalImportPanel { HostServices = services, OutputDirectory = directory ?? services?.CurrentWorkingDirectory ?? Environment.CurrentDirectory };
    /// <summary>
    /// Creates the menu item that opens the plugin panel.
    /// </summary>
    /// <returns>The resulting tool strip menu item.</returns>
    public ToolStripMenuItem CreateMenuItem()
    {
        var menu = new ToolStripMenuItem(Name);
        menu.DropDownItems.Add("Analyze source", null, (_, _) => (CreatePanel() as ExternalImportPanel)!.Analyze());
        return menu;
    }
    /// <summary>
    /// Updates the plugin for the newly opened project.
    /// </summary>
    /// <param name="projectPath">The project path.</param>
    public void OnProjectOpened(string projectPath)
    {
        OnProjectClosed(); directory = projectPath;
        if (panel is not null && !panel.IsDisposed) panel.OutputDirectory = projectPath;
    }
    /// <summary>
    /// Clears plugin state associated with the closed project.
    /// </summary>
    public void OnProjectClosed()
    {
        directory = null;
        if (panel is not null && !panel.IsDisposed) { panel.Cancel(); panel.OutputDirectory = string.Empty; }
    }
}
