using System.Reflection;
using Gondwana.Configuration;
using Gondwana.Input.Gamepad;
using Gondwana.Timers;
using Gondwana.WinForms.Input.Gamepad.XInput;
using static Gondwana.WinForms.Input.Gamepad.XInput.XInput;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for gamepad input.
/// </summary>
[Collection("Global engine state")]
public sealed class GamepadInputTests
{
    /// <summary>
    /// Verifies engine configuration uses requested gamepad cadence defaults.
    /// </summary>
    [Fact]
    public void EngineConfiguration_UsesRequestedGamepadCadenceDefaults()
    {
        var configuration = new EngineConfiguration();

        Assert.Equal(0.2d, configuration.GamepadConnectionUpdateFrequencyHz);
        Assert.Equal(60d, configuration.GamepadPollFrequencyHz);
    }

    /// <summary>
    /// Verifies initialize with no gamepad argument preserves configured manager.
    /// </summary>
    [Fact]
    public void Initialize_WithNoGamepadArgument_PreservesConfiguredManager()
    {
        var engine = CreateEngineInstance();
        var manager = new TestGamepadManager();
        var missingConfigPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            engine.Input.GamepadManager = manager;
            engine.Initialize(configFileName: missingConfigPath);

            Assert.Same(manager, engine.Input.GamepadManager);
        }
        finally
        {
            engine.Input.GamepadManager = null;
            engine.Dispose();
            GC.SuppressFinalize(engine);
        }
    }

    /// <summary>
    /// Verifies refresh gamepads uses independent connection and polling cadences.
    /// </summary>
    [Fact]
    public void RefreshGamepads_UsesIndependentConnectionAndPollingCadences()
    {
        var input = new EngineInputSystems();
        var manager = new TestGamepadManager();
        var configuration = new EngineConfiguration
        {
            TargetFPS = 0,
            GamepadConnectionUpdateFrequencyHz = 0.2d,
            GamepadPollFrequencyHz = 2d
        };

        try
        {
            input.GamepadManager = manager;
            manager.ResetCounts();

            long start = HighResTimer.GetCurrentTick();

            input.RefreshGamepads(start + HighResTimer.TicksPerSecond / 4, configuration);
            Assert.Equal(0, manager.ConnectionUpdateCount);
            Assert.Equal(0, manager.PollCount);

            input.RefreshGamepads(start + (HighResTimer.TicksPerSecond * 3 / 4), configuration);
            Assert.Equal(0, manager.ConnectionUpdateCount);
            Assert.Equal(1, manager.PollCount);

            input.RefreshGamepads(start + (HighResTimer.TicksPerSecond * 21 / 4), configuration);
            Assert.Equal(1, manager.ConnectionUpdateCount);
            Assert.Equal(2, manager.PollCount);
        }
        finally
        {
            input.GamepadManager = null;
        }
    }

    /// <summary>
    /// Verifies refresh gamepads after discovery refreshes event poller snapshot without resetting bindings.
    /// </summary>
    [Fact]
    public void RefreshGamepads_AfterDiscovery_RefreshesEventPollerSnapshotWithoutResettingBindings()
    {
        var input = new EngineInputSystems();
        var manager = new TestGamepadManager();
        var configuration = new EngineConfiguration
        {
            GamepadConnectionUpdateFrequencyHz = 1d,
            GamepadPollFrequencyHz = 0d
        };

        try
        {
            input.GamepadManager = manager;

            var poller = input.GamepadEventPoller!;
            poller.StartMonitoringButton(manager.Adapter.GamepadId, "A");

            var secondAdapter = new TestGamepadAdapter("test-gamepad-2");
            manager.AddAdapter(secondAdapter);
            manager.ResetCounts();

            long tick = HighResTimer.GetCurrentTick() + (HighResTimer.TicksPerSecond * 2);
            input.RefreshGamepads(tick, configuration);

            Assert.Equal(1, manager.ConnectionUpdateCount);
            Assert.Same(poller, input.GamepadEventPoller);
            Assert.Contains(secondAdapter, poller.Adapters!);
            Assert.True(
                poller.AllButtonConfigsByGamepadId[manager.Adapter.GamepadId].ContainsKey("A"));
        }
        finally
        {
            input.GamepadManager = null;
        }
    }

    /// <summary>
    /// Verifies run simulation cycle uses driver tick for gamepad hardware cadence.
    /// </summary>
    [Fact]
    public void RunSimulationCycle_UsesDriverTickForGamepadHardwareCadence()
    {
        var engine = CreateEngineInstance();
        var manager = new TestGamepadManager();

        try
        {
            engine.Input.GamepadManager = manager;
            manager.ResetCounts();

            engine.Configuration.GamepadConnectionUpdateFrequencyHz = 0.2d;
            engine.Configuration.GamepadPollFrequencyHz = 60d;

            long now = HighResTimer.GetCurrentTick();
            long driverTick = now + (HighResTimer.TicksPerSecond * 6);

            InvokeRunSimulationCycle(
                engine,
                simulationTick: now + (HighResTimer.TicksPerSecond / 10),
                renderTick: driverTick);

            InvokeRunSimulationCycle(
                engine,
                simulationTick: now + (HighResTimer.TicksPerSecond / 5),
                renderTick: driverTick);

            Assert.Equal(1, manager.ConnectionUpdateCount);
            Assert.Equal(1, manager.PollCount);
        }
        finally
        {
            engine.Input.GamepadManager = null;
            GC.SuppressFinalize(engine);
        }
    }

    /// <summary>
    /// Verifies run simulation cycle polls gamepad before game callbacks and button events.
    /// </summary>
    [Fact]
    public void RunSimulationCycle_PollsGamepadBeforeGameCallbacksAndButtonEvents()
    {
        var engine = CreateEngineInstance();
        var manager = new TestGamepadManager();
        var order = new List<string>();

        try
        {
            engine.Input.GamepadManager = manager;
            manager.ResetCounts();

            engine.Configuration.GamepadConnectionUpdateFrequencyHz = 0d;
            engine.Configuration.GamepadPollFrequencyHz = 60d;

            var poller = engine.Input.GamepadEventPoller!;
            poller.StartMonitoringButton(manager.Adapter.GamepadId, "A", timeBetweenEvents: 0d);

            manager.OnPoll = () =>
            {
                order.Add("poll");
                manager.Adapter.SetPressedButtons("A");
            };
            engine.BeforeBackgroundTasksExecute += () => order.Add("game");
            poller.ButtonDown += _ => order.Add("event");

            long tick = HighResTimer.GetCurrentTick() + HighResTimer.TicksPerSecond;
            InvokeRunSimulationCycle(engine, simulationTick: tick, renderTick: tick);

            Assert.Equal(["poll", "game", "event"], order);
        }
        finally
        {
            engine.Input.GamepadManager = null;
            GC.SuppressFinalize(engine);
        }
    }

    /// <summary>
    /// Verifies x input state projection uses current trigger values.
    /// </summary>
    [Fact]
    public void XInputStateProjection_UsesCurrentTriggerValues()
    {
        var adapter = new XInputGamepadAdapter();

        adapter.ApplyState(new XINPUT_GAMEPAD
        {
            wButtons = (ushort)XInputButtons.A,
            bLeftTrigger = 200,
            bRightTrigger = 32
        });

        Assert.Contains("A", adapter.PressedButtons);
        Assert.Contains("LeftTrigger", adapter.PressedButtons);
        Assert.DoesNotContain("RightTrigger", adapter.PressedButtons);
        Assert.Equal(200f / 255f, adapter.LeftTrigger);
        Assert.Equal(32f / 255f, adapter.RightTrigger);

        adapter.ApplyState(new XINPUT_GAMEPAD
        {
            bLeftTrigger = 0,
            bRightTrigger = 220
        });

        Assert.DoesNotContain("LeftTrigger", adapter.PressedButtons);
        Assert.Contains("RightTrigger", adapter.PressedButtons);
        Assert.Equal(0f, adapter.LeftTrigger);
        Assert.Equal(220f / 255f, adapter.RightTrigger);

        adapter.ClearState();

        Assert.Empty(adapter.PressedButtons);
        Assert.Null(adapter.LeftStick);
        Assert.Null(adapter.RightStick);
        Assert.Equal(0f, adapter.LeftTrigger);
        Assert.Equal(0f, adapter.RightTrigger);
    }

    private static Engine CreateEngineInstance() =>
        (Engine)Activator.CreateInstance(typeof(Engine), nonPublic: true)!;

    private static void InvokeRunSimulationCycle(
        Engine engine,
        long simulationTick,
        long renderTick)
    {
        var method = typeof(Engine).GetMethod(
            "RunSimulationCycle",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine.RunSimulationCycle via reflection.");

        method.Invoke(
            engine,
            [
                simulationTick,
                1d / 60d,
                false,
                renderTick,
                1d / 60d,
                false
            ]);
    }

    private sealed class TestGamepadManager : IGamepadManager<IGamepadAdapter>
    {
        private readonly List<IGamepadAdapter> _adapters;

        /// <summary>
        /// Initializes a new instance of the <c>TestGamepadManager</c> class.
        /// </summary>
        public TestGamepadManager()
        {
            Adapter = new TestGamepadAdapter();
            _adapters = [Adapter];
        }

        /// <summary>
        /// Gets the adapter.
        /// </summary>
        public TestGamepadAdapter Adapter { get; }

        /// <inheritdoc/>
        public IReadOnlyCollection<IGamepadAdapter> ConnectedAdapters => _adapters.ToArray();

        /// <summary>
        /// Gets the connection update count.
        /// </summary>
        public int ConnectionUpdateCount { get; private set; }

        /// <summary>
        /// Gets the poll count.
        /// </summary>
        public int PollCount { get; private set; }

        /// <summary>
        /// Gets or sets the on poll.
        /// </summary>
        public Action? OnPoll { get; set; }

        /// <inheritdoc/>
        public void UpdateConnections()
        {
            ConnectionUpdateCount++;
        }

        /// <inheritdoc/>
        public void Poll()
        {
            PollCount++;
            OnPoll?.Invoke();
        }

#pragma warning disable CS0618
        /// <inheritdoc/>
        public void Update()
        {
            UpdateConnections();
            Poll();
        }
#pragma warning restore CS0618

        /// <summary>
        /// Adds adapter.
        /// </summary>
        /// <param name="adapter">The adapter value for this test case.</param>
        public void AddAdapter(IGamepadAdapter adapter)
        {
            _adapters.Add(adapter);
        }

        /// <summary>
        /// Resets counts.
        /// </summary>
        public void ResetCounts()
        {
            ConnectionUpdateCount = 0;
            PollCount = 0;
            OnPoll = null;
            Adapter.SetPressedButtons();
        }
    }

    private sealed class TestGamepadAdapter : IGamepadAdapter
    {
        private readonly HashSet<string> _pressedButtons = new(StringComparer.Ordinal);

        private readonly string _gamepadId;

        /// <summary>
        /// Initializes a new instance of the <c>TestGamepadAdapter</c> class.
        /// </summary>
        /// <param name="gamepadId">The gamepad id value for this test case.</param>
        public TestGamepadAdapter(string gamepadId = "test-gamepad")
        {
            _gamepadId = gamepadId;
        }

        /// <inheritdoc/>
        public string GamepadId => _gamepadId;

        /// <inheritdoc/>
        public IReadOnlyCollection<string> PressedButtons => _pressedButtons;

        /// <inheritdoc/>
        public GamepadStickState? LeftStick => null;

        /// <inheritdoc/>
        public GamepadStickState? RightStick => null;

        /// <inheritdoc/>
        public float LeftTrigger => 0f;

        /// <inheritdoc/>
        public float RightTrigger => 0f;

        /// <summary>
        /// Sets pressed buttons.
        /// </summary>
        /// <param name="buttons">The buttons value for this test case.</param>
        public void SetPressedButtons(params string[] buttons)
        {
            _pressedButtons.Clear();
            _pressedButtons.UnionWith(buttons);
        }
    }
}
