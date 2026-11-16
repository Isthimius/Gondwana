namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Opt-in contract for receiving Studio services after discovery and before UI contributions are created.</summary>
public interface IStudioPluginHostServicesAware
{
    /// <summary>Attaches the Studio services used by this plugin.</summary>
    /// <param name="services">Borrowed host services; invoke them only on the host UI thread.</param>
    void AttachHostServices(IStudioPluginHostServices services);
}
