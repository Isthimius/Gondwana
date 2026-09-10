using System.Reflection;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class EngineInitializationTests
{
    [Fact]
    public void Initialize_WhenInitializationThrows_ResetsInitializationStateAndSignalsCompletion()
    {
        var engine = CreateEngineInstance();
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
            GC.SuppressFinalize(engine);
            File.Delete(invalidConfigPath);
        }
    }

    [Fact]
    public void Initialize_WhenPreviousInitializationFailed_CanRetrySuccessfully()
    {
        var engine = CreateEngineInstance();
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
            GC.SuppressFinalize(engine);
            File.Delete(invalidConfigPath);
        }
    }

    [Fact]
    public void Start_WhenInitializationInProgress_UsesConfiguredWaitTimeout()
    {
        var engine = CreateEngineInstance();

        try
        {
            engine.Configuration.StartInitializationWaitTimeout = 0.001f;
            SetInitializationState(engine, isInitializing: true, isInitialized: false);
            GetInitDoneEvent(engine).Reset();

            var exception = Assert.Throws<InvalidOperationException>(
                () => engine.Start(new SynchronizationContext()));

            Assert.Contains("did not complete within", exception.Message);
            Assert.Contains("0.001 seconds", exception.Message);
        }
        finally
        {
            GC.SuppressFinalize(engine);
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

    private static void SetInitializationState(Engine engine, bool isInitializing, bool isInitialized)
    {
        var initializingField = typeof(Engine).GetField(
            "_isInitializing",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine._isInitializing via reflection.");

        var initializedField = typeof(Engine).GetField(
            "_isInitialized",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine._isInitialized via reflection.");

        initializingField.SetValue(engine, isInitializing);
        initializedField.SetValue(engine, isInitialized);
    }
}
