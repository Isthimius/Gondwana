using System.Reflection;
using Gondwana.Timers;
using EngineTimer = Gondwana.Timers.Timer;

namespace Gondwana.Tests;

/// <summary>
/// Contains unit tests for the <see cref="EngineTimer"/> class.
/// </summary>
[Collection("NonParallelTimerTests")]
public sealed class TimerTests : IDisposable
{
    /// <summary>
    /// Initializes the timer registry to a known state before each test.
    /// </summary>
    public TimerTests()
    {
        EngineTimer.ClearAll();
        EngineTimer.PausedAll = false;
    }

    /// <summary>
    /// Verifies that adding a timer with an explicit identifier registers it for lookup.
    /// </summary>
    [Fact]
    public void AddAndGet_WithExplicitId_RegistersTimer()
    {
        var timer = EngineTimer.Add("explicit", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        Assert.Equal("explicit", timer.TimerID);
        Assert.Same(timer, EngineTimer.Get("explicit"));
        Assert.True(timer.Length > 0);
        Assert.Equal(1, EngineTimer.Count);
    }

    /// <summary>
    /// Verifies that adding a timer without an identifier generates and registers one.
    /// </summary>
    [Fact]
    public void AddWithoutId_GeneratesAndRegistersTimer()
    {
        var timer = EngineTimer.Add(TimerType.PostCycle, TimerCycles.Once, 0.01);

        Assert.False(string.IsNullOrWhiteSpace(timer.TimerID));
        Assert.Contains(timer.TimerID, EngineTimer.TimerIDs);
    }

    /// <summary>
    /// Verifies that removing an existing or missing timer identifier is safe.
    /// </summary>
    [Fact]
    public void Remove_ExistingAndMissingId_AreSafe()
    {
        EngineTimer.Add("to-remove", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        EngineTimer.Remove("to-remove");
        EngineTimer.Remove("missing");

        Assert.Equal(0, EngineTimer.Count);
    }

    /// <summary>
    /// Verifies that clearing all timers removes every registered timer.
    /// </summary>
    [Fact]
    public void ClearAll_RemovesAllTimers()
    {
        EngineTimer.Add("a", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        EngineTimer.Add("b", TimerType.PostCycle, TimerCycles.Once, 0.01);

        EngineTimer.ClearAll();

        Assert.Equal(0, EngineTimer.Count);
        Assert.Empty(EngineTimer.TimerIDs);
    }

    /// <summary>
    /// Verifies that disposing a timer removes it from the registry.
    /// </summary>
    [Fact]
    public void Dispose_RemovesTimer()
    {
        var timer = EngineTimer.Add("disposable", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        timer.Dispose();
        timer.Dispose();

        Assert.Equal(0, EngineTimer.Count);
        Assert.DoesNotContain("disposable", EngineTimer.TimerIDs);
    }

    /// <summary>
    /// Verifies that raising timer events invokes the tick handler for timers of the matching type.
    /// </summary>
    [Fact]
    public void RaiseTimerEvents_InvokesTickForMatchingType()
    {
        var timer = EngineTimer.Add("pre", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.True(ticks >= 1);
    }

    /// <summary>
    /// Verifies that raising timer events does not invoke timers of a different type.
    /// </summary>
    [Fact]
    public void RaiseTimerEvents_DoesNotInvokeWhenTypeDiffers()
    {
        var timer = EngineTimer.Add("post", TimerType.PostCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(0, ticks);
    }

    /// <summary>
    /// Verifies that a once timer is removed after its tick event is raised.
    /// </summary>
    [Fact]
    public void RaiseTimerEvents_OnceTimerIsRemovedAfterTick()
    {
        var timer = EngineTimer.Add("once", TimerType.PreCycle, TimerCycles.Once, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = GetLastEventTick(timer) + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(1, ticks);
        Assert.DoesNotContain("once", EngineTimer.TimerIDs);
    }

    /// <summary>
    /// Verifies that pausing all timers prevents tick events from being raised.
    /// </summary>
    [Fact]
    public void RaiseTimerEvents_PausedAll_PreventsTick()
    {
        var timer = EngineTimer.Add("paused", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;
        EngineTimer.PausedAll = true;

        var engineTick = HighResTimer.GetCurrentTick() + (timer.Length * 3);
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(0, ticks);
    }

    /// <summary>
    /// Verifies that repeating timers catch up on missed intervals when events are raised late.
    /// </summary>
    [Fact]
    public void RaiseTimerEvents_RepeatingTimerCatchesUp()
    {
        var timer = EngineTimer.Add("repeat", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + (timer.Length * 3);
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.True(ticks >= 3);
    }

    /// <summary>
    /// Releases resources used by this test fixture.
    /// </summary>
    public void Dispose()
    {
        EngineTimer.ClearAll();
        EngineTimer.PausedAll = false;
    }

    private static void InvokeRaiseTimerEvents(TimerType type, long engineTick)
    {
        var method = typeof(EngineTimer).GetMethod("RaiseTimerEvents", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find Timer.RaiseTimerEvents via reflection.");
        method.Invoke(null, [type, engineTick]);
    }

    private static long GetLastEventTick(EngineTimer timer)
    {
        var property = typeof(EngineTimer).GetProperty("_lastEventTick", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find Timer._lastEventTick via reflection.");
        return (long)(property.GetValue(timer) ?? throw new InvalidOperationException("Timer._lastEventTick is null."));
    }
}

/// <summary>
/// Defines the non-parallelized test collection used by <see cref="TimerTests"/>.
/// </summary>
[CollectionDefinition("NonParallelTimerTests", DisableParallelization = true)]
public sealed class NonParallelTimerTestsCollectionDefinition;
