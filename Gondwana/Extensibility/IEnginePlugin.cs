namespace Gondwana.Extensibility;

public interface IEnginePlugin
{
    string Name { get; }
    string Version { get; }

    void OnInitialize(Engine engine);
    void OnPreCycle(Engine engine, double deltaMs);
    void OnPostCycle(Engine engine, double deltaMs);
    void OnShutdown(Engine engine);
}
