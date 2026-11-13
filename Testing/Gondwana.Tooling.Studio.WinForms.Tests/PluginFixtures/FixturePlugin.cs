using Gondwana.Tooling.Studio.Core.Extensibility;

namespace StudioPluginFixtures;

public sealed class FixturePlugin : IStudioPlugin
{
    public string Name => PrivateDependency.Version;
    public void OnProjectOpened(string projectPath) { }
    public void OnProjectClosed() { }
}
