using Gondwana.Hosting;

namespace Gondwana.Tests;

public sealed class GameHostBaseCompatibilityTests
{
    [Fact]
    public void LegacyInitializeEngineOverload_RemainsAvailableToDerivedHosts()
    {
        using var host = new LegacyInitializeEngineHost();

        Assert.IsAssignableFrom<GameHostBase>(host);
    }

    private sealed class LegacyInitializeEngineHost : GameHostBase
    {
        public void CompileLegacyInitializeEngineCall(string? configPath, bool? autoSaveConfig)
            => InitializeEngine(configPath, autoSaveConfig);

        protected override void ConfigurePlatform()
        {
        }
    }
}
