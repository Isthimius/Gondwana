namespace Gondwana.Extensibility;

/// <summary>
/// IEnginePlugin.
/// </summary>
public interface IEnginePlugin
{
    string Name { get; }
    string Version { get; }

    void OnInitialize(Engine engine);
    void OnPreCycle(Engine engine, double deltaMs);
    void OnPreFrameRender(Engine engine, double deltaMs);
    void OnPostFrameRender(Engine engine, double deltaMs);
    void OnPostCycle(Engine engine, double deltaMs);
    void OnShutdown(Engine engine);
}
