using System.Text.Json.Serialization;
using Gondwana.Rendering;
using Gondwana.State;
using Gondwana.Timers;

namespace Gondwana.Configuration;

/// <summary>
/// Settings used by the engine when cycling
/// </summary>
public partial class EngineConfiguration
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

    /// <summary>
    /// Minimum time (in seconds) allowed between Mouse events.
    /// Use this to prevent flooding the system with too many events at once (holding down a button, dragging, etc).
    /// Default is 0.03 seconds (30 milliseconds).
    /// </summary>
    public double TimeBetweenMouseEvents { get; set; } = 0.03;

    /// <summary>
    /// Gets or sets a value indicating whether the Backbuffer should be recreated when the RenderSurfaceAdapterBase is resized.
    /// </summary>
    public bool RecreateBackbufferOnResize { get; set; } = true;

    /// <summary>
    /// Optional collection of serialized <see cref="EngineState"/>s to mount at initialization.
    /// </summary>
    public List<string>? StateFiles { get; set; }
}
