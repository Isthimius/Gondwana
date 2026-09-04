namespace Gondwana.Timers;

/// <summary>
/// Supplies the fixed simulation clock to update-side objects while timer-driven mode is active.
/// </summary>
internal static class EngineSimulationClock
{
    private static long _simulationTick = HighResTimer.GetCurrentTick();
    private static int _timerDriven;

    /// <summary>Gets the current simulation tick, or wall-clock time outside timer-driven mode.</summary>
    internal static long GetCurrentTick() => Volatile.Read(ref _timerDriven) != 0
        ? Interlocked.Read(ref _simulationTick)
        : HighResTimer.GetCurrentTick();

    /// <summary>Begins timer-driven operation at the supplied tick.</summary>
    internal static void BeginTimerDriven(long tick)
    {
        Interlocked.Exchange(ref _simulationTick, tick);
        Volatile.Write(ref _timerDriven, 1);
    }

    /// <summary>Advances the timer-driven simulation clock to a scheduled fixed step.</summary>
    internal static void SetTimerDrivenTick(long tick) =>
        Interlocked.Exchange(ref _simulationTick, tick);

    /// <summary>Restores wall-clock reads for the desktop/background-loop mode.</summary>
    internal static void UseWallClock() => Volatile.Write(ref _timerDriven, 0);
}
