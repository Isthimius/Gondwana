using System.Reflection;
using Gondwana.Timers;
using EngineTimer = Gondwana.Timers.Timer;

namespace Gondwana.Tests;

public sealed class TimerTests : IDisposable
{
    public TimerTests()
    {
        EngineTimer.ClearAll();
        EngineTimer.PausedAll = false;
    }

    [Fact]
    public void AddAndGet_WithExplicitId_RegistersTimer()
    {
        var timer = EngineTimer.Add("explicit", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        Assert.Equal("explicit", timer.TimerID);
        Assert.Same(timer, EngineTimer.Get("explicit"));
        Assert.True(timer.Length > 0);
        Assert.Equal(1, EngineTimer.Count);
    }

    [Fact]
    public void AddWithoutId_GeneratesAndRegistersTimer()
    {
        var timer = EngineTimer.Add(TimerType.PostCycle, TimerCycles.Once, 0.01);

        Assert.False(string.IsNullOrWhiteSpace(timer.TimerID));
        Assert.Contains(timer.TimerID, EngineTimer.TimerIDs);
    }

    [Fact]
    public void Remove_ExistingAndMissingId_AreSafe()
    {
        EngineTimer.Add("to-remove", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        EngineTimer.Remove("to-remove");
        EngineTimer.Remove("missing");

        Assert.Equal(0, EngineTimer.Count);
    }

    [Fact]
    public void ClearAll_RemovesAllTimers()
    {
        EngineTimer.Add("a", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        EngineTimer.Add("b", TimerType.PostCycle, TimerCycles.Once, 0.01);

        EngineTimer.ClearAll();

        Assert.Equal(0, EngineTimer.Count);
        Assert.Empty(EngineTimer.TimerIDs);
    }

    [Fact]
    public void Dispose_RemovesTimer()
    {
        var timer = EngineTimer.Add("disposable", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        timer.Dispose();
        timer.Dispose();

        Assert.Equal(0, EngineTimer.Count);
        Assert.DoesNotContain("disposable", EngineTimer.TimerIDs);
    }

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

    [Fact]
    public void RaiseTimerEvents_OnceTimerIsRemovedAfterTick()
    {
        var timer = EngineTimer.Add("once", TimerType.PreCycle, TimerCycles.Once, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(1, ticks);
        Assert.DoesNotContain("once", EngineTimer.TimerIDs);
    }

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
}
