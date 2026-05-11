using System.Reflection;
using Gondwana.Timers;

namespace Gondwana.Tests;

public sealed class TimerTests : IDisposable
{
    public TimerTests()
    {
        Timer.ClearAll();
        Timer.PausedAll = false;
    }

    [Fact]
    public void AddAndGet_WithExplicitId_RegistersTimer()
    {
        var timer = Timer.Add("explicit", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        Assert.Equal("explicit", timer.TimerID);
        Assert.Same(timer, Timer.Get("explicit"));
        Assert.True(timer.Length > 0);
        Assert.Equal(1, Timer.Count);
    }

    [Fact]
    public void AddWithoutId_GeneratesAndRegistersTimer()
    {
        var timer = Timer.Add(TimerType.PostCycle, TimerCycles.Once, 0.01);

        Assert.False(string.IsNullOrWhiteSpace(timer.TimerID));
        Assert.Contains(timer.TimerID, Timer.TimerIDs);
    }

    [Fact]
    public void Remove_ExistingAndMissingId_AreSafe()
    {
        Timer.Add("to-remove", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        Timer.Remove("to-remove");
        Timer.Remove("missing");

        Assert.Equal(0, Timer.Count);
    }

    [Fact]
    public void ClearAll_RemovesAllTimers()
    {
        Timer.Add("a", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        Timer.Add("b", TimerType.PostCycle, TimerCycles.Once, 0.01);

        Timer.ClearAll();

        Assert.Equal(0, Timer.Count);
        Assert.Empty(Timer.TimerIDs);
    }

    [Fact]
    public void Dispose_RemovesTimer()
    {
        var timer = Timer.Add("disposable", TimerType.PreCycle, TimerCycles.Repeating, 0.01);

        timer.Dispose();
        timer.Dispose();

        Assert.Equal(0, Timer.Count);
        Assert.DoesNotContain("disposable", Timer.TimerIDs);
    }

    [Fact]
    public void RaiseTimerEvents_InvokesTickForMatchingType()
    {
        var timer = Timer.Add("pre", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.True(ticks >= 1);
    }

    [Fact]
    public void RaiseTimerEvents_DoesNotInvokeWhenTypeDiffers()
    {
        var timer = Timer.Add("post", TimerType.PostCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(0, ticks);
    }

    [Fact]
    public void RaiseTimerEvents_OnceTimerIsRemovedAfterTick()
    {
        var timer = Timer.Add("once", TimerType.PreCycle, TimerCycles.Once, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + timer.Length;
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(1, ticks);
        Assert.DoesNotContain("once", Timer.TimerIDs);
    }

    [Fact]
    public void RaiseTimerEvents_PausedAll_PreventsTick()
    {
        var timer = Timer.Add("paused", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;
        Timer.PausedAll = true;

        var engineTick = HighResTimer.GetCurrentTick() + (timer.Length * 3);
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.Equal(0, ticks);
    }

    [Fact]
    public void RaiseTimerEvents_RepeatingTimerCatchesUp()
    {
        var timer = Timer.Add("repeat", TimerType.PreCycle, TimerCycles.Repeating, 0.01);
        var ticks = 0;
        timer.Tick += () => ticks++;

        var engineTick = HighResTimer.GetCurrentTick() + (timer.Length * 3);
        InvokeRaiseTimerEvents(TimerType.PreCycle, engineTick);

        Assert.True(ticks >= 3);
    }

    public void Dispose()
    {
        Timer.ClearAll();
        Timer.PausedAll = false;
    }

    private static void InvokeRaiseTimerEvents(TimerType type, long engineTick)
    {
        var method = typeof(Timer).GetMethod("RaiseTimerEvents", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find Timer.RaiseTimerEvents via reflection.");
        method.Invoke(null, [type, engineTick]);
    }
}
