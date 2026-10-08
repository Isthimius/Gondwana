using Gondwana.Hosting;
using Microsoft.Extensions.Logging;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for game host base compatibility.
/// </summary>
public sealed class GameHostBaseCompatibilityTests
{
    /// <summary>
    /// Verifies initialize legacy metadata signature remains available.
    /// </summary>
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

    /// <summary>
    /// Verifies legacy initialize engine overload remains available to derived hosts.
    /// </summary>
    [Fact]
    public void LegacyInitializeEngineOverload_RemainsAvailableToDerivedHosts()
    {
        using var host = new LegacyInitializeEngineHost();

        Assert.IsAssignableFrom<GameHostBase>(host);
    }

    private sealed class LegacyInitializeEngineHost : GameHostBase
    {
        /// <summary>
        /// Checks that the legacy engine initialization call remains source-compatible.
        /// </summary>
        /// <param name="configPath">The config path value for this test case.</param>
        /// <param name="autoSaveConfig">The auto save config value for this test case.</param>
        public void CompileLegacyInitializeEngineCall(string? configPath, bool? autoSaveConfig)
            => InitializeEngine(configPath, autoSaveConfig);

        /// <inheritdoc/>
        protected override void ConfigurePlatform()
        {
        }
    }
}
