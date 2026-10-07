using System.Reflection;
using Gondwana.Configuration;
using Gondwana.Input.Gamepad;
using Gondwana.Timers;
using Gondwana.WinForms.Input.Gamepad.XInput;
using static Gondwana.WinForms.Input.Gamepad.XInput.XInput;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class GamepadInputTests
{
    [Fact]
    public void EngineConfiguration_UsesRequestedGamepadCadenceDefaults()
    {
        var configuration = new EngineConfiguration();

        Assert.Equal(0.2d, configuration.GamepadConnectionUpdateFrequencyHz);
        Assert.Equal(60d, configuration.GamepadPollFrequencyHz);
    }

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

    [Fact]
    public void RefreshGamepads_UsesIndependentConnectionAndPollingCadences()
    {
        var input = new EngineInputSystems();
        var manager = new TestGamepadManager();
        var configuration = new EngineConfiguration
        {
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
            InvokeRunSimulationCycle(engine, tick);

            Assert.Equal(["poll", "game", "event"], order);
        }
        finally
        {
            engine.Input.GamepadManager = null;
            GC.SuppressFinalize(engine);
        }
    }

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

    private static void InvokeRunSimulationCycle(Engine engine, long tick)
    {
        var method = typeof(Engine).GetMethod(
            "RunSimulationCycle",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find Engine.RunSimulationCycle via reflection.");

        method.Invoke(
            engine,
            [
                tick,
                1d / 60d,
                false,
                tick,
                1d / 60d,
                false
            ]);
    }

    private sealed class TestGamepadManager : IGamepadManager<IGamepadAdapter>
    {
        private readonly IGamepadAdapter[] _adapters;

        public TestGamepadManager()
        {
            Adapter = new TestGamepadAdapter();
            _adapters = [Adapter];
        }

        public TestGamepadAdapter Adapter { get; }

        public IReadOnlyCollection<IGamepadAdapter> ConnectedAdapters => _adapters;

        public int ConnectionUpdateCount { get; private set; }

        public int PollCount { get; private set; }

        public Action? OnPoll { get; set; }

        public void UpdateConnections()
        {
            ConnectionUpdateCount++;
        }

        public void Poll()
        {
            PollCount++;
            OnPoll?.Invoke();
        }

#pragma warning disable CS0618
        public void Update()
        {
            UpdateConnections();
            Poll();
        }
#pragma warning restore CS0618

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

        public string GamepadId => "test-gamepad";

        public IReadOnlyCollection<string> PressedButtons => _pressedButtons;

        public GamepadStickState? LeftStick => null;

        public GamepadStickState? RightStick => null;

        public float LeftTrigger => 0f;

        public float RightTrigger => 0f;

        public void SetPressedButtons(params string[] buttons)
        {
            _pressedButtons.Clear();
            _pressedButtons.UnionWith(buttons);
        }
    }
}
