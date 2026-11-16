using CorePlugin = Gondwana.Tooling.Studio.Core.Extensibility.IStudioPlugin;

namespace Gondwana.Tooling.Studio.WinForms.Extensibility;

/// <summary>
/// WinForms-specific studio plugin interface.
/// Extends the framework-neutral <see cref="CorePlugin"/> with WinForms UI contributions.
/// </summary>
public interface IStudioPlugin : CorePlugin
{
    /// <summary>Supplies an optional control for a global Studio tool pane on the UI thread.</summary>
    /// <returns>The control to host, or <see langword="null"/> if the plugin contributes no pane.</returns>
    /// <remarks>Studio parents and themes the control and disposes it with the shell. Closing the pane hides it.</remarks>
    Control? CreatePanel();

    /// <summary>Supplies an optional top-level item for Studio's Plugins menu on the UI thread.</summary>
    /// <returns>The menu item owned by the shell after insertion, or <see langword="null"/> if none.</returns>
    ToolStripMenuItem? CreateMenuItem();
}
