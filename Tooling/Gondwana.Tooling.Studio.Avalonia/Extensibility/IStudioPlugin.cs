using Avalonia.Controls;
using CorePlugin = Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.Avalonia.Extensibility;

/// <summary>
/// Defines extensibility points for contributing panels, menu items, and project lifecycle behavior to Gondwana Studio.
/// Extends the framework-neutral <see cref="CorePlugin"/> with Avalonia-specific UI contributions.
/// </summary>
/// <remarks>
/// Implementations can be discovered by the studio plugin host and participate in project open/close
/// events while optionally providing UI contributions.
/// </remarks>
public interface IStudioPlugin : CorePlugin
{
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
}
