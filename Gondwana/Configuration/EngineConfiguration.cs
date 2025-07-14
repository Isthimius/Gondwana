using System.Text.Json.Serialization;
using Gondwana.Rendering;
using Gondwana.Resource;
using Gondwana.State;
using Gondwana.Timers;

namespace Gondwana.Configuration;

/// <summary>
/// Settings used by the engine when cycling
/// </summary>
public class EngineConfiguration
{
    private int _targetFPS = 60;

    /// <summary>
    /// Target screen refresh rate for the Engine. Setting the number
    /// lower allows more time for the processor to perform background
    /// Engine tasks. Set the value to 0 for no upper limit.
    /// Default is 60 FPS.
    /// </summary>
    public int TargetFPS
    {
        get => _targetFPS;
        set => _targetFPS = value < 0 ? 0 : value;
    }

    private double _samplingTimeForCPS = 1.5;

    /// <summary>
    /// Total number of seconds between Cycles Per Second (CPS) calculation.
    /// Default is 1.5 seconds.
    /// </summary>
    public double SamplingTimeForCPS
    {
        get => _samplingTimeForCPS;
        set => _samplingTimeForCPS = value < 0 ? 0 : value;
    }

    /// <summary>
    /// Total number of system ticks between each CPS sampling.
    /// </summary>
    [JsonIgnore]
    public long SamplingTimeForCPSTicks => (long)(SamplingTimeForCPS * HighResTimer.TicksPerSecond);

    /// <summary>
    /// Minimum time (in seconds) allowed between Keyboard events.
    /// Use this to prevent flooding the system with too many events at once (holding down a key, etc).
    /// Default is 0.03 seconds (30 milliseconds).
    /// </summary>
    public double TimeBetweenKeyboardEvents { get; set; } = 0.03;

    /// <summary>
    /// Minimum time (in seconds) allowed between Gamepad events.
    /// Use this to prevent flooding the system with too many events at once (holding down a button, etc).
    /// Default is 0.03 seconds (30 milliseconds).
    /// </summary>
    public double TimeBetweenGamepadEvents { get; set; } = 0.03;

    private double _visibleSurfaceRefreshTimer = 1.5;

    /// <summary>
    /// Time in seconds of forced refresh of entire area of all VisibleSurface instances.
    /// Use this to force a full redraw of all visible surfaces, such as when the game window is resized,
    /// or is partially obscured by another window.
    /// Default is 1.5 seconds.
    /// </summary>
    public double VisibleSurfaceRefreshTimer
    {
        get => _visibleSurfaceRefreshTimer;
        set
        {
            _visibleSurfaceRefreshTimer = value;
            VisibleSurfaces.ForcedRefreshRate = value;
        }
    }

    /// <summary>
    /// Total number of resized Frame stretched renderings allowed in cache.
    /// Lowering this value may degrade performance, but lessen required system memory.
    /// Only change if necessary for performance optimization. Default is 100.
    /// </summary>
    public int ResizedFrameCacheLimit { get; set; } = 100;

    /// <summary>
    /// Optional collection of serialized <see cref="EngineState"/>s to mount at initialization.
    /// </summary>
    public List<string>? StateFiles { get; set; }
}
