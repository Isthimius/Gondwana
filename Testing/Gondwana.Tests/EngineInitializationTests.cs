using System.Reflection;
using Gondwana.Configuration;

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
    public void Initialize_WithConfigurationStore_UsesAndPersistsSuppliedConfiguration()
    {
        var engine = CreateEngineInstance();
        var store = new TestConfigurationStore
        {
            AutoSave = true
        };
        store.Configuration.TargetFPS = 37;

        try
        {
            engine.Initialize(configurationStore: store);

            Assert.Same(store.Configuration, engine.Configuration);
            Assert.Equal(37, engine.Configuration.TargetFPS);

            engine.Configuration.TargetFPS = 73;
            engine.SaveConfiguration();

            Assert.Equal(1, store.SaveCount);
            Assert.Equal(73, store.LastSavedTargetFps);
        }
        finally
        {
            engine.Dispose();
            Assert.True(store.IsDisposed);
            Assert.Equal(2, store.SaveCount);
            GC.SuppressFinalize(engine);
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
            var expectedSeconds = engine.Configuration.StartInitializationWaitTimeout
                .ToString("0.###", System.Globalization.CultureInfo.CurrentCulture);
            Assert.Contains($"{expectedSeconds} seconds", exception.Message);
        }
        finally
        {
            GC.SuppressFinalize(engine);
        }
    }

    [Fact]
    public async Task Dispose_WhenCalledOnEngineThread_DoesNotWaitForOwnCycleTask()
    {
        var engine = CreateEngineInstance();
        Task? cycleTask = null;
        var disposeReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            SetIsRunning(engine, true);

            cycleTask = Task.Run(() =>
            {
                Assert.True(SpinWait.SpinUntil(() => cycleTask is not null, TimeSpan.FromSeconds(1)));

                engine.EngineDispatcher.BindToCurrentThread();
                SetCycleTask(engine, cycleTask!);

                engine.Dispose();
                disposeReturned.SetResult();
            });

            await disposeReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await cycleTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(SpinWait.SpinUntil(() => engine.IsDisposed, TimeSpan.FromSeconds(2)));
        }
        finally
        {
            GC.SuppressFinalize(engine);
        }
    }

    [Fact]
    public async Task Dispose_WhenCalledInsideCycleOnEngineThread_DefersManagedCleanupUntilCycleReturns()
    {
        var engine = CreateEngineInstance();
        Task? cycleTask = null;
        var disposeObservedInsideCycle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            SetIsRunning(engine, true);
            engine.Configuration.SamplingTimeForCPS = 0;
            engine.BeforeBackgroundTasksExecute += () =>
            {
                engine.Dispose();
                disposeObservedInsideCycle.SetResult(engine.IsDisposed);
            };

            cycleTask = Task.Run(() =>
            {
                Assert.True(SpinWait.SpinUntil(() => cycleTask is not null, TimeSpan.FromSeconds(1)));

                engine.EngineDispatcher.BindToCurrentThread();
                SetCycleTask(engine, cycleTask!);
                InvokeCycle(engine);
            });

            var wasDisposedInsideCycle = await disposeObservedInsideCycle.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await cycleTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.False(wasDisposedInsideCycle);
            Assert.True(SpinWait.SpinUntil(() => engine.IsDisposed, TimeSpan.FromSeconds(2)));
        }
        finally
        {
            GC.SuppressFinalize(engine);
        }
    }

    [Fact]
    public async Task StopAndWait_WhenAlreadyStopped_WaitsForPendingCycle()
    {
        var engine = CreateEngineInstance();
        var cycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetCycleTask(engine, cycle.Task);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopping = Task.Run(() => { entered.SetResult(); engine.StopAndWait(); });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => stopping.WaitAsync(TimeSpan.FromMilliseconds(100)));
            cycle.SetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(engine.IsDisposed);
        }
        finally
        {
            cycle.TrySetResult();
            await stopping;
            GC.SuppressFinalize(engine);
        }
    }

    [Fact]
    public void StopAndWait_OnActiveEngineThread_RejectsSelfWait()
    {
        var engine = CreateEngineInstance();
        try
        {
            engine.EngineDispatcher.BindToCurrentThread();
            SetCycleTask(engine, new TaskCompletionSource().Task);
            Assert.Throws<InvalidOperationException>(() => engine.StopAndWait());
        }
        finally { GC.SuppressFinalize(engine); }
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

    private static void SetCycleTask(Engine engine, Task cycleTask)
    {
        var cycleTaskField = typeof(Engine).GetField(
            "_cycleTask",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine._cycleTask via reflection.");

        cycleTaskField.SetValue(engine, cycleTask);
    }

    private static void SetIsRunning(Engine engine, bool isRunning)
    {
        var property = typeof(Engine).GetProperty(
            nameof(Engine.IsRunning),
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Could not find Engine.IsRunning via reflection.");

        property.SetValue(engine, isRunning);
    }

    private sealed class TestConfigurationStore : IEngineConfigurationStore
    {
        public EngineConfiguration Configuration { get; } = new();

        public bool AutoSave { get; set; }

        public int SaveCount { get; private set; }

        public int LastSavedTargetFps { get; private set; }

        public bool IsDisposed { get; private set; }

        public void Save()
        {
            SaveCount++;
            LastSavedTargetFps = Configuration.TargetFPS;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            if (AutoSave)
                Save();

            IsDisposed = true;
        }
    }

    private static void InvokeCycle(Engine engine)
    {
        var cycleMethod = typeof(Engine).GetMethod(
            "Cycle",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine.Cycle via reflection.");

        cycleMethod.Invoke(engine, null);
    }
}
