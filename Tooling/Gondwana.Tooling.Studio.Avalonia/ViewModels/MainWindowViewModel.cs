using Dock.Model.Controls;
using Gondwana.Tooling.Studio.Avalonia.Docking;
using Gondwana.Tooling.Studio.Avalonia.Extensibility;
using Gondwana.Tooling.Studio.ViewModels;

namespace Gondwana.Tooling.Studio.Avalonia.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the dock factory and layout.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Gets get.
    /// </summary>
    public StudioDockFactory Factory { get; }
    /// <summary>
    /// Gets get.
    /// </summary>
    public IRootDock Layout { get; }
    /// <summary>
    /// Gets get.
    /// </summary>
    public DirectoryPanelViewModel DirectoryPanel { get; }
    /// <summary>
    /// Gets get.
    /// </summary>
    public OutputViewModel Output { get; }
    /// <summary>
    /// Gets get.
    /// </summary>
    public StudioPluginHost PluginHost { get; }
    /// <summary>
    /// Gets or sets set.
    /// </summary>
    public string? CurrentProjectPath { get; private set; }

    /// <summary>
    /// MainWindowViewModel.
    /// </summary>
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
    /// <param name="id">id.</param>
    /// <param name="title">title.</param>
    /// <param name="context">context.</param>
    public void OpenDocumentTab(string id, string title, object context)
    {
        Factory.OpenDocument(id, title, context);
    }

    /// <summary>
    /// SetProject.
    /// </summary>
    /// <param name="projectPath">projectPath.</param>
    public void SetProject(string projectPath)
    {
        if (!string.IsNullOrWhiteSpace(CurrentProjectPath) && !string.Equals(CurrentProjectPath, projectPath, StringComparison.Ordinal))
            CloseProject();

        CurrentProjectPath = projectPath;
        PluginHost.NotifyProjectOpened(projectPath);
        Output.Log($"Project opened: {projectPath}");
    }

    /// <summary>
    /// CloseProject.
    /// </summary>
    public void CloseProject()
    {
        if (CurrentProjectPath is null)
            return;

        PluginHost.NotifyProjectClosed();
        Output.Log($"Project closed: {CurrentProjectPath}");
        CurrentProjectPath = null;
    }
}
