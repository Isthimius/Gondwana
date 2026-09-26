namespace Gondwana.Configuration;

/// <summary>
/// Provides a persistence boundary for <see cref="EngineConfiguration"/>.
/// </summary>
/// <remarks>
/// The engine owns the store supplied during initialization and disposes it during
/// reinitialization or shutdown. Stores may persist to files, browser storage, or
/// another platform-specific backing service.
/// </remarks>
public interface IEngineConfigurationStore : IDisposable
{
    /// <summary>Gets the configuration represented by this store.</summary>
    EngineConfiguration Configuration { get; }

    /// <summary>Gets or sets whether the store saves automatically when disposed.</summary>
    bool AutoSave { get; set; }

    /// <summary>Persists the current <see cref="Configuration"/>.</summary>
    void Save();
}
