using Gondwana.Rendering;
using Gondwana.Timers;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Gondwana.Configuration;

/// <summary>
/// Settings used by the engine when cycling
/// </summary>
public class EngineSettings
{
    private int _targetFPS = 60;

    /// <summary>
    /// Target screen refresh rate for the Engine. Setting the number
    /// lower allows more time for the processor to perform background
    /// Engine tasks. Set the value to 0 for no upper limit.
    /// </summary>
    public int TargetFPS
    {
        get => _targetFPS;
        set => _targetFPS = value < 0 ? 0 : value;
    }

    private double _samplingTimeForCPS = 1.5;

    /// <summary>
    /// Total number of seconds between Cycles Per Second (CPS) calculation
    /// </summary>
    public double SamplingTimeForCPS
    {
        get => _samplingTimeForCPS;
        set => _samplingTimeForCPS = value < 0 ? 0 : value;
    }

    /// <summary>
    /// Total number of system ticks between each CPS sampling
    /// </summary>
    [JsonIgnore]
    public long SamplingTimeForCPSTicks => (long)(SamplingTimeForCPS * HighResTimer.TicksPerSecond);

    /// <summary>
    /// Minimum time (in seconds) allowed between Keyboard events
    /// </summary>
    public double TimeBetweenKeyboardEvents { get; set; } = 0.03;

    private double _visibleSurfaceRefreshTimer = 1.5;

    /// <summary>
    /// Time in seconds of forced refresh of entire area of all VisibleSurface instances
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
    /// Total number of resized Frame stretched renderings allowed in cache.  Lowering this value may degrade performance, but lessen required system memory.
    /// </summary>
    public int ResizedFrameCacheLimit { get; set; } = 100;

    [OnDeserialized]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        // Re-trigger setter logic to recalculate dependent fields
        TargetFPS = _targetFPS;
        SamplingTimeForCPS = _samplingTimeForCPS;
    }
}
