namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>
/// Framework-neutral interface for Gondwana Studio plugins.
/// Platform-specific extensions (Avalonia, WinForms) extend this interface
/// to add UI panel and menu-item contributions.
/// </summary>
public interface IStudioPlugin
{
    /// <summary>Gets the display name of the plugin.</summary>
    string Name { get; }

    /// <summary>Notifies the plugin that a project has been opened.</summary>
    /// <param name="projectPath">The path of the project that was opened.</param>
    void OnProjectOpened(string projectPath);

    /// <summary>Notifies the plugin that the current project has been closed.</summary>
    void OnProjectClosed();
}
