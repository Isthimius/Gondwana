using Gondwana.Hosting;
using Microsoft.Extensions.Logging;

namespace Gondwana.Tests;

public sealed class GameHostBaseCompatibilityTests
{
    [Fact]
    public void Initialize_LegacyMetadataSignature_RemainsAvailable()
    {
        var method = typeof(GameHostBase).GetMethod(
            nameof(GameHostBase.Initialize),
            [
                typeof(string),
                typeof(bool?),
                typeof(LogLevel)
            ]);

        Assert.NotNull(method);
    }

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
