using Dock.Model.Controls;
using Gondwana.Studio.Docking;
using Gondwana.Studio.Extensibility;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the dock factory and layout.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    public StudioDockFactory Factory { get; }
    public IRootDock Layout { get; }
    public DirectoryPanelViewModel DirectoryPanel { get; }
    public OutputViewModel Output { get; }
    public StudioPluginHost PluginHost { get; }
    public string? CurrentProjectPath { get; private set; }

    public MainWindowViewModel()
    {
        DirectoryPanel = new DirectoryPanelViewModel();
        Output = new OutputViewModel();
        PluginHost = new StudioPluginHost(message => Output.Log(message));

        Factory = new StudioDockFactory(DirectoryPanel, Output);
        Layout = Factory.CreateLayout();
        Factory.InitLayout(Layout);

        PluginHost.DiscoverAndLoad();
    }

    /// <summary>
    /// Opens a document tab. Called from code-behind when the owner Window is available.
    /// </summary>
    public void OpenDocumentTab(string id, string title, object context)
    {
        Factory.OpenDocument(id, title, context);
    }

    public void SetProject(string projectPath)
    {
        if (!string.IsNullOrWhiteSpace(CurrentProjectPath) && !string.Equals(CurrentProjectPath, projectPath, StringComparison.Ordinal))
            CloseProject();

        CurrentProjectPath = projectPath;
        PluginHost.NotifyProjectOpened(projectPath);
        Output.Log($"Project opened: {projectPath}");
    }

    public void CloseProject()
    {
        if (CurrentProjectPath is null)
            return;

        PluginHost.NotifyProjectClosed();
        Output.Log($"Project closed: {CurrentProjectPath}");
        CurrentProjectPath = null;
    }
}
