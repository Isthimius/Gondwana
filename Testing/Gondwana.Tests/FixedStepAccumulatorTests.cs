using Gondwana.Configuration;
using Gondwana.Timers;

namespace Gondwana.Tests;

/// <summary>Tests timer-driven fixed-step scheduling independently of the engine singleton.</summary>
public sealed class FixedStepAccumulatorTests
{
    private const int UpdateRate = 120;

    /// <summary>Irregular driver ticks retain their fractional remainder across frames.</summary>
    [Fact]
    public void Advance_RetainsFractionalRemainderAcrossFrames()
    {
        long stepTicks = GetStepTicks();
        const long startTick = 1_000;
        var accumulator = new FixedStepAccumulator();
        accumulator.Reset(startTick);

        var early = accumulator.Advance(startTick + stepTicks - 1, UpdateRate, maxSteps: 8);
        var first = accumulator.Advance(startTick + stepTicks + 1, UpdateRate, maxSteps: 8);
        var second = accumulator.Advance(startTick + (2 * stepTicks), UpdateRate, maxSteps: 8);

        Assert.Equal(0, early.StepCount);
        Assert.Equal(1, first.StepCount);
        Assert.Equal(startTick + stepTicks, first.GetStepTick(0));
        Assert.Equal(1, second.StepCount);
        Assert.Equal(startTick + (2 * stepTicks), second.GetStepTick(0));
    }

    /// <summary>A delayed driver frame is bounded and excess backlog is discarded.</summary>
    [Fact]
    public void Advance_CapsCatchUpAndDiscardsExcessBacklog()
    {
        long stepTicks = GetStepTicks();
        const long startTick = 5_000;
        var accumulator = new FixedStepAccumulator();
        accumulator.Reset(startTick);

        var delayed = accumulator.Advance(startTick + (20 * stepTicks), UpdateRate, maxSteps: 8);
        var next = accumulator.Advance(startTick + (21 * stepTicks), UpdateRate, maxSteps: 8);

        Assert.Equal(8, delayed.StepCount);
        Assert.Equal(startTick + stepTicks, delayed.GetStepTick(0));
        Assert.Equal(startTick + (8 * stepTicks), delayed.GetStepTick(7));
        Assert.Equal(1, next.StepCount);
        Assert.Equal(startTick + (9 * stepTicks), next.GetStepTick(0));
    }

    /// <summary>Non-monotonic driver timestamps do not rewind or add simulation time.</summary>
    [Fact]
    public void Advance_NonMonotonicDriverTickDoesNotRewind()
    {
        const long startTick = 10_000;
        var accumulator = new FixedStepAccumulator();
        accumulator.Reset(startTick);

        var batch = accumulator.Advance(startTick - 1, UpdateRate, maxSteps: 8);

        Assert.Equal(0, batch.StepCount);
        Assert.Equal(startTick, accumulator.SimulationTick);
    }

    /// <summary>Timer-driven scheduling configuration always remains usable.</summary>
    [Fact]
    public void EngineConfiguration_ClampsTimerDrivenSchedulingValues()
    {
        var configuration = new EngineConfiguration
        {
            TimerDrivenSimulationRate = 0,
            MaxTimerDrivenSimulationSteps = -1
        };

        Assert.Equal(1, configuration.TimerDrivenSimulationRate);
        Assert.Equal(1, configuration.MaxTimerDrivenSimulationSteps);
    }

    private static long GetStepTicks() =>
        Math.Max(1, HighResTimer.TicksPerSecond / UpdateRate);
}
