namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Opt-in extension; existing IStudioPlugin implementations remain unchanged.</summary>
public interface IStudioPluginHostServicesAware
{
    /// <summary>
    /// Attaches the Studio services used by this plugin.
    /// </summary>
    /// <param name="services">The services.</param>
    void AttachHostServices(IStudioPluginHostServices services);
}
