using CorePlugin = Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.WinForms.Extensibility;

/// <summary>
/// WinForms-specific studio plugin interface.
/// Extends the framework-neutral <see cref="CorePlugin"/> with WinForms UI contributions.
/// </summary>
public interface IStudioPlugin : CorePlugin
{
    /// <summary>
    /// Creates an optional dockable panel control for the plugin.
    /// </summary>
    /// <returns>The <see cref="Control"/> to host, or <see langword="null"/> if the plugin provides none.</returns>
    Control? CreatePanel();

    /// <summary>
    /// Creates an optional menu item for the plugin.
    /// </summary>
    /// <returns>The <see cref="ToolStripMenuItem"/> to add, or <see langword="null"/> if none.</returns>
    ToolStripMenuItem? CreateMenuItem();
}
