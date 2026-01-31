namespace Gondwana.Timers
{
    /// <summary>
    /// Specifies when a <see cref="Timer"/> should have its <see cref="Timer.Tick"/> event raised
    /// relative to the engine's main update and render cycle. This controls the execution timing
    /// of timer events within each engine cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine's cycle is divided into two main phases: background tasks (which include input polling,
    /// animation updates, and movement processing) and foreground tasks (which include rendering and
    /// presentation). Timers can be configured to execute during either phase.
    /// </para>
    /// <para>
    /// <see cref="PreCycle"/> timers are raised during the background task phase before any rendering
    /// occurs, making them suitable for game logic, state updates, and input processing.
    /// <see cref="PostCycle"/> timers are raised after the foreground render phase completes, making
    /// them suitable for post-render operations, diagnostics, and deferred cleanup tasks.
    /// </para>
    /// </remarks>
    public enum TimerType
    {
        /// <summary>
        /// The timer's <see cref="Timer.Tick"/> event is raised during the background task phase,
        /// before the engine performs its foreground rendering operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PreCycle timers execute during the engine's background task phase, which includes:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Input polling (keyboard, mouse, gamepad)</description></item>
        /// <item><description>Tile animation cycling</description></item>
        /// <item><description>Sprite movement processing</description></item>
        /// <item><description>Collision detection and resolution</description></item>
        /// <item><description>Camera updates</description></item>
        /// </list>
        /// <para>
        /// This timing is appropriate for game logic updates, AI processing, state changes,
        /// and any operations that need to complete before the current frame is rendered.
        /// PreCycle timers run at the engine's full update rate, which may be higher than
        /// the render frame rate depending on configuration.
        /// </para>
        /// </remarks>
        PreCycle,

        /// <summary>
        /// The timer's <see cref="Timer.Tick"/> event is raised after the foreground rendering
        /// and presentation phase completes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PostCycle timers execute after the engine has completed all foreground tasks, including:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Direct drawing updates</description></item>
        /// <item><description>Render surface backbuffer rendering</description></item>
        /// <item><description>Backbuffer presentation to display adapters</description></item>
        /// <item><description>Gamepad state updates</description></item>
        /// </list>
        /// <para>
        /// This timing is appropriate for post-render operations such as performance monitoring,
        /// screenshot capture, frame statistics collection, deferred resource cleanup, or any
        /// operations that should occur after a complete frame has been rendered and presented.
        /// PostCycle timers run at the effective render frame rate (FPS), which may be throttled
        /// by the engine's target FPS setting.
        /// </para>
        /// </remarks>
        PostCycle
    }
}