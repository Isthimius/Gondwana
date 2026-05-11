using System.Globalization;
using Gondwana;
using Gondwana.Timers;

namespace Gondwana.Tests;

public sealed class CoreUtilityTests
{
    [Fact]
    public void CyclesPerSecondCalculatedEventArgs_StoresConstructorValues()
    {
        var args = new CyclesPerSecondCalculatedEventArgs(100, 90, 60.5, 54.25, 1.5, 58.75);

        Assert.Equal(100, args.TotalGrossCycles);
        Assert.Equal(90, args.TotalNetCycles);
        Assert.Equal(60.5, args.GrossCPS);
        Assert.Equal(54.25, args.NetCPS);
        Assert.Equal(1.5, args.SampleTime);
        Assert.Equal(58.75, args.GpuFps);
    }

    [Fact]
    public void CyclesPerSecondCalculatedEventArgs_ToString_FormatsWithGpuFps()
    {
        var args = new CyclesPerSecondCalculatedEventArgs(1000, 900, 60, 54, 2, 58.5);
        var culture = CultureInfo.CurrentCulture;

        var text = args.ToString();

        Assert.Contains($"Total gross cycles: {1000.ToString("N0", culture)}", text);
        Assert.Contains($"Total net cycles: {900.ToString("N0", culture)}", text);
        Assert.Contains($"Sampling time: {2d.ToString("N2", culture)}s", text);
        Assert.Contains($"Gross CPS: {60d.ToString("N2", culture)}", text);
        Assert.Contains($"Net CPS (FPS): {54d.ToString("N2", culture)}", text);
        Assert.Contains($"GPU FPS: {58.5d.ToString("N2", culture)}", text);
    }

    [Fact]
    public void CyclesPerSecondCalculatedEventArgs_ToString_OmitsGpuFpsWhenNull()
    {
        var args = new CyclesPerSecondCalculatedEventArgs(10, 9, 6, 5.4, 1);

        var text = args.ToString();

        Assert.DoesNotContain("GPU FPS", text);
    }

    [Fact]
    public void EngineStateParts_AllContainsEveryFlag()
    {
        var all = EngineStateParts.All;

        Assert.True(all.HasFlag(EngineStateParts.AssetsFiles));
        Assert.True(all.HasFlag(EngineStateParts.Tilesheets));
        Assert.True(all.HasFlag(EngineStateParts.Cycles));
        Assert.True(all.HasFlag(EngineStateParts.Scenes));
        Assert.True(all.HasFlag(EngineStateParts.Sprites));
        Assert.True(all.HasFlag(EngineStateParts.Audio));
    }

    [Fact]
    public void HighResTimer_GetDurationAndElapsedSince_AreNonNegative()
    {
        var start = HighResTimer.GetCurrentTick();
        var stop = start + HighResTimer.TicksPerSecond;

        var duration = HighResTimer.GetDuration(start, stop);
        var elapsed = HighResTimer.GetElapsedSince(start);

        Assert.Equal(1f, duration, 3);
        Assert.True(elapsed >= 0f);
        Assert.True(HighResTimer.TicksPerSecond > 0);
    }
}
