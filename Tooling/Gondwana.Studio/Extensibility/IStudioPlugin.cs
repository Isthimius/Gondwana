using Avalonia.Controls;

namespace Gondwana.Studio.Extensibility;

public interface IStudioPlugin
{
    string Name { get; }
    Control? CreatePanel();
    MenuItem? CreateMenuItem();
    void OnProjectOpened(string projectPath);
    void OnProjectClosed();
}
