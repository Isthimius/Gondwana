using Avalonia.Controls;

namespace Gondwana.Studio.Extensibility;

/// <summary>
/// Defines extensibility points for contributing panels, menu items, and project lifecycle behavior to Gondwana Studio.
/// </summary>
/// <remarks>
/// Implementations can be discovered by the studio plugin host and participate in project open/close
/// events while optionally providing UI contributions.
/// </remarks>
public interface IStudioPlugin
{
    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates an optional dockable panel for the plugin.
    /// </summary>
    /// <returns>The panel control to host, or <see langword="null"/> if the plugin does not provide one.</returns>
    Control? CreatePanel();

    /// <summary>
    /// Creates an optional menu item for the plugin.
    /// </summary>
    /// <returns>The menu item to add to the studio UI, or <see langword="null"/> if none is provided.</returns>
    MenuItem? CreateMenuItem();

    /// <summary>
    /// Notifies the plugin that a project has been opened.
    /// </summary>
    /// <param name="projectPath">The path of the project that was opened.</param>
    void OnProjectOpened(string projectPath);

    /// <summary>
    /// Notifies the plugin that the current project has been closed.
    /// </summary>
    void OnProjectClosed();
}
