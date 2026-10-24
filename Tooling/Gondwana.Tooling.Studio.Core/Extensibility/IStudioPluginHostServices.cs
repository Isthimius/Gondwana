namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Optional Studio document services. Call only on the host UI thread.</summary>
public interface IStudioPluginHostServices
{
    /// <summary>Gets the full path currently displayed by Studio's working-directory browser.</summary>
    string CurrentWorkingDirectory { get; }

    /// <summary>Appends a timestamped message to Studio's Output pane.</summary>
    /// <param name="message">The diagnostic text to display.</param>
    void Log(string message);

    /// <summary>
    /// Refreshes the host's working-directory listing.
    /// </summary>
    void RefreshWorkingDirectory();

    /// <summary>Opens a supported loose document or activates its existing Studio tab.</summary>
    /// <param name="path">The document file path; relative paths use the process's current directory.</param>
    /// <remarks>Uses the normal editor loader and may prompt for an archive password. Load failures propagate to the caller.</remarks>
    /// <exception cref="NotSupportedException">The file's authoring format is unsupported.</exception>
    void OpenDocument(string path);
}
