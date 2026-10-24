namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>
/// Framework-neutral interface for Gondwana Studio plugins.
/// Platform-specific extensions such as WinForms extend this interface
/// to add UI panel and menu-item contributions.
/// </summary>
public interface IStudioPlugin
{
    /// <summary>Gets the display name of the plugin.</summary>
    string Name { get; }

    /// <summary>Notifies the plugin that Studio has selected a working directory.</summary>
    /// <param name="projectPath">The full path to the selected authoring directory, not a project file.</param>
    void OnProjectOpened(string projectPath);

    /// <summary>Notifies the plugin to release working-directory state when Studio switches directories or shuts down.</summary>
    void OnProjectClosed();
}
