namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Optional Studio document services. Call only on the host UI thread.</summary>
public interface IStudioPluginHostServices
{
    /// <summary>
    /// Gets the current working directory.
    /// </summary>
    string CurrentWorkingDirectory { get; }
    /// <summary>
    /// Writes a message to the logging destination.
    /// </summary>
    /// <param name="message">The message.</param>
    void Log(string message);
    /// <summary>
    /// Refreshes the host's working-directory listing.
    /// </summary>
    void RefreshWorkingDirectory();
    /// <summary>
    /// Opens a document in the Studio host.
    /// </summary>
    /// <param name="path">The path.</param>
    void OpenDocument(string path);
}

/// <summary>Opt-in extension; existing IStudioPlugin implementations remain unchanged.</summary>
public interface IStudioPluginHostServicesAware
{
    /// <summary>
    /// Attaches the Studio services used by this plugin.
    /// </summary>
    /// <param name="services">The services.</param>
    void AttachHostServices(IStudioPluginHostServices services);
}
