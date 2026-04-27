using Dock.Model.Controls;
using Gondwana.Studio.Docking;

namespace Gondwana.Studio.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the dock factory and layout.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    public StudioDockFactory Factory { get; }
    public IRootDock Layout { get; }
    public DirectoryPanelViewModel DirectoryPanel { get; }

    public MainWindowViewModel()
    {
        DirectoryPanel = new DirectoryPanelViewModel();

        Factory = new StudioDockFactory(DirectoryPanel);
        Layout = Factory.CreateLayout();
        Factory.InitLayout(Layout);
    }

    /// <summary>
    /// Opens a document tab. Called from code-behind when the owner Window is available.
    /// </summary>
    public void OpenDocumentTab(string id, string title, object context)
    {
        Factory.OpenDocument(id, title, context);
    }
}
