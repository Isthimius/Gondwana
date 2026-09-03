namespace Gondwana.Timers;

/// <summary>
/// Converts irregular externally driven ticks into bounded batches of fixed simulation steps.
/// </summary>
internal sealed class FixedStepAccumulator
{
    private long _lastDriverTick;
    private long _accumulatedTicks;

    /// <summary>Gets the tick represented by the most recently scheduled simulation step.</summary>
    internal long SimulationTick { get; private set; }

    /// <summary>Resets both the driver and simulation clocks to <paramref name="tick"/>.</summary>
    internal void Reset(long tick)
    {
        _lastDriverTick = tick;
        _accumulatedTicks = 0;
        SimulationTick = tick;
    }

    /// <summary>
    /// Accumulates driver time and returns the fixed steps due for the next browser frame.
    /// Excess backlog is discarded after <paramref name="maxSteps"/> steps to prevent a spiral
    /// of death after throttling or tab suspension.
    /// </summary>
    internal FixedStepBatch Advance(long driverTick, int updatesPerSecond, int maxSteps)
    {
        if (updatesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(updatesPerSecond));
        if (maxSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSteps));

        long stepTicks = Math.Max(1, HighResTimer.TicksPerSecond / updatesPerSecond);
        long elapsedTicks = driverTick > _lastDriverTick
            ? driverTick - _lastDriverTick
            : 0;

        if (driverTick > _lastDriverTick)
            _lastDriverTick = driverTick;

        long maxAccumulatedTicks = stepTicks > long.MaxValue / maxSteps
            ? long.MaxValue
            : stepTicks * maxSteps;

        _accumulatedTicks = elapsedTicks >= maxAccumulatedTicks - _accumulatedTicks
            ? maxAccumulatedTicks
            : _accumulatedTicks + elapsedTicks;

        int stepCount = (int)Math.Min(maxSteps, _accumulatedTicks / stepTicks);
        _accumulatedTicks -= stepCount * stepTicks;

        long firstStepTick = SimulationTick + stepTicks;
        SimulationTick += stepCount * stepTicks;

        return new FixedStepBatch(firstStepTick, stepTicks, stepCount);
    }
}

/// <summary>Describes one bounded batch of fixed simulation steps.</summary>
internal readonly record struct FixedStepBatch(long FirstStepTick, long StepTicks, int StepCount)
{
    /// <summary>Gets the absolute simulation tick for the zero-based step index.</summary>
    internal long GetStepTick(int index)
    {
        if ((uint)index >= (uint)StepCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return FirstStepTick + (index * StepTicks);
    }
}
