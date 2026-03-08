namespace Gondwana.Timers
{
    /// <summary>
    /// Specifies the cycle behavior of a <see cref="Timer"/>, determining whether it executes
    /// once or repeatedly at its configured interval.
    /// </summary>
    /// <remarks>
    /// This enumeration is used when creating timers to control their lifecycle behavior.
    /// A <see cref="Once"/> timer will automatically be removed after its first execution,
    /// while a <see cref="Repeating"/> timer will continue to raise events until explicitly
    /// removed or disposed.
    /// </remarks>
    public enum TimerCycles
    {
        /// <summary>
        /// The timer executes only once. After the first <see cref="Timer.Tick"/> event is raised,
        /// the timer is automatically disposed and removed from the active timer collection.
        /// This is useful for delayed one-time actions or deferred execution scenarios.
        /// </summary>
        Once,

        /// <summary>
        /// The timer executes repeatedly at its configured interval. The <see cref="Timer.Tick"/>
        /// event is raised each time the interval elapses, and the timer continues executing
        /// until it is explicitly removed via <see cref="Timer.Remove"/> or <see cref="Timer.Dispose"/>.
        /// This is useful for periodic updates, polling operations, or recurring game logic.
        /// </summary>
        Repeating
    }
}