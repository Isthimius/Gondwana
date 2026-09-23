namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Optional Studio document services. Call only on the host UI thread.</summary>
public interface IStudioPluginHostServices
{
    string CurrentWorkingDirectory { get; }
    void Log(string message);
    void RefreshWorkingDirectory();
    void OpenDocument(string path);
}

/// <summary>Opt-in extension; existing IStudioPlugin implementations remain unchanged.</summary>
public interface IStudioPluginHostServicesAware
{
    void AttachHostServices(IStudioPluginHostServices services);
}
