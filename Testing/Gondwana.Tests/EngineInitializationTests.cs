using System.Reflection;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class EngineInitializationTests
{
    [Fact]
    public void Initialize_WhenInitializationThrows_ResetsInitializationStateAndSignalsCompletion()
    {
        using var engine = CreateEngineInstance();
        var invalidConfigPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(invalidConfigPath, "{");

        try
        {
            Assert.ThrowsAny<Exception>(() => engine.Initialize(configFileName: invalidConfigPath));

            Assert.False(engine.IsInitializing);
            Assert.False(engine.IsInitialized);
            Assert.True(GetInitDoneEvent(engine).IsSet);
        }
        finally
        {
            File.Delete(invalidConfigPath);
        }
    }

    [Fact]
    public void Initialize_WhenPreviousInitializationFailed_CanRetrySuccessfully()
    {
        using var engine = CreateEngineInstance();
        var invalidConfigPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(invalidConfigPath, "{");

        try
        {
            Assert.ThrowsAny<Exception>(() => engine.Initialize(configFileName: invalidConfigPath));

            var missingConfigPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            engine.Initialize(configFileName: missingConfigPath);

            Assert.False(engine.IsInitializing);
            Assert.True(engine.IsInitialized);
        }
        finally
        {
            File.Delete(invalidConfigPath);
        }
    }

    private static Engine CreateEngineInstance() =>
        (Engine)Activator.CreateInstance(typeof(Engine), nonPublic: true)!;

    private static ManualResetEventSlim GetInitDoneEvent(Engine engine)
    {
        var field = typeof(Engine).GetField(
            "_initDone",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine._initDone via reflection.");

        return (ManualResetEventSlim)(field.GetValue(engine)
            ?? throw new InvalidOperationException("Engine._initDone is null."));
    }
}
