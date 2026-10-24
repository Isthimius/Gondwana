using Gondwana.Tooling.Studio.Core.Extensibility;
using IStudioPlugin = Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.Plugin.ExternalImport;

/// <summary>Contributes the external-asset import panel and analysis command to Studio.</summary>
public sealed class ExternalImportPlugin : IStudioPlugin, IStudioPluginHostServicesAware
{
    private ExternalImportPanel? panel;
    private IStudioPluginHostServices? services;
    private string? directory;
    /// <summary>Gets the display label used for the import tool pane and its Plugins menu entry.</summary>
    public string Name => "External Asset Importer";
    /// <summary>Retains host services and applies them to the import panel if already created.</summary>
    /// <param name="hostServices">Borrowed Studio services to use on the UI thread.</param>
    public void AttachHostServices(IStudioPluginHostServices hostServices)
    {
        services = hostServices;
        if (panel is not null) panel.HostServices = services;
    }
    /// <summary>Creates the import panel once, choosing its initial output directory from the project, host, or process.</summary>
    /// <returns>The cached panel, which Studio owns and disposes after hosting it.</returns>
    public Control CreatePanel() => panel ??= new ExternalImportPanel { HostServices = services, OutputDirectory = directory ?? services?.CurrentWorkingDirectory ?? Environment.CurrentDirectory };
    /// <summary>Creates a Plugins menu entry containing an Analyze source command.</summary>
    /// <returns>A new menu item owned by its caller; the command starts analysis in the cached import panel.</returns>
    public ToolStripMenuItem CreateMenuItem()
    {
        var menu = new ToolStripMenuItem(Name);
        menu.DropDownItems.Add("Analyze source", null, (_, _) => (CreatePanel() as ExternalImportPanel)!.Analyze());
        return menu;
    }
    /// <summary>Cancels prior work and uses the newly selected working directory for import output.</summary>
    /// <param name="projectPath">The authoring directory to retain and show as the output directory.</param>
    public void OnProjectOpened(string projectPath)
    {
        OnProjectClosed(); directory = projectPath;
        if (panel is not null && !panel.IsDisposed) panel.OutputDirectory = projectPath;
    }
    /// <summary>Forgets the working directory, cancels tracked import work, and clears the live panel's output directory.</summary>
    public void OnProjectClosed()
    {
        directory = null;
        if (panel is not null && !panel.IsDisposed) { panel.Cancel(); panel.OutputDirectory = string.Empty; }
    }
}
