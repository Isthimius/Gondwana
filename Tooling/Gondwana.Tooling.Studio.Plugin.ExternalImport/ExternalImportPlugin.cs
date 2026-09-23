using Gondwana.Tooling.Studio.Core.Extensibility;
using IStudioPlugin = Gondwana.Tooling.Studio.WinForms.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.Plugin.ExternalImport;

public sealed class ExternalImportPlugin : IStudioPlugin, IStudioPluginHostServicesAware
{
    private ExternalImportPanel? panel;
    private IStudioPluginHostServices? services;
    private string? directory;
    public string Name => "External Asset Importer";
    public void AttachHostServices(IStudioPluginHostServices hostServices)
    {
        services = hostServices;
        if (panel is not null) panel.HostServices = services;
    }
    public Control CreatePanel() => panel ??= new ExternalImportPanel { HostServices = services, OutputDirectory = directory ?? services?.CurrentWorkingDirectory ?? Environment.CurrentDirectory };
    public ToolStripMenuItem CreateMenuItem()
    {
        var menu = new ToolStripMenuItem(Name);
        menu.DropDownItems.Add("Analyze source", null, (_, _) => (CreatePanel() as ExternalImportPanel)!.Analyze());
        return menu;
    }
    public void OnProjectOpened(string projectPath)
    {
        OnProjectClosed(); directory = projectPath;
        if (panel is not null && !panel.IsDisposed) panel.OutputDirectory = projectPath;
    }
    public void OnProjectClosed()
    {
        directory = null;
        if (panel is not null && !panel.IsDisposed) { panel.Cancel(); panel.OutputDirectory = string.Empty; }
    }
}
