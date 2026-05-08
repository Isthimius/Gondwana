using Avalonia.Controls;

namespace Gondwana.Studio.Extensibility;

/// <summary>
/// IStudioPlugin.
/// </summary>
public interface IStudioPlugin
{
    string Name { get; }
    Control? CreatePanel();
    MenuItem? CreateMenuItem();
    void OnProjectOpened(string projectPath);
    void OnProjectClosed();
}
