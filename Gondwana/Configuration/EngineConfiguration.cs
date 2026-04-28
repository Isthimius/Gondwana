using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Timers;
using Newtonsoft.Json;

namespace Gondwana.Configuration;

/// <summary>
/// Settings used by the engine when cycling
/// </summary>
[JsonObject(IsReference = true)]
public partial class EngineConfiguration
{
    private int _targetFPS = 60;

    /// <summary>
    /// Target screen refresh rate for the Engine. Setting the number
    /// lower allows more time for the processor to perform background
    /// Engine tasks. Set the value to 0 for no upper limit.
    /// Default is 60 FPS.
    /// </summary>
    /// <remarks>
    /// Setting this property also updates <see cref="Gondwana.Rendering.Backbuffers.GpuBackbuffer.TargetFps"/>
    /// on all currently registered GPU surfaces so that the render timer interval stays consistent.
    /// </remarks>
    public int TargetFPS
    {
        get => _targetFPS;
        set
        {
            _targetFPS = value < 0 ? 0 : value;

            // Propagate to all active GPU backbuffers so their render timer intervals stay in sync.
            foreach (var surface in RenderSurfaceHostRegistry.All)
            {
                if (surface.Backbuffer is GpuBackbuffer gpuBb)
                    gpuBb.TargetFps = _targetFPS;
            }
        }
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
    /// Controls whether engine logging operations are performed synchronously or asynchronously.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="EngineLoggingMode.Asynchronous"/>, log entries are enqueued and
    /// processed on a background thread as a fire-and-forget operation. This avoids blocking
    /// the engine's main loop but may drop log records if the queue is saturated.
    ///
    /// When set to <see cref="EngineLoggingMode.Synchronous"/>, log entries are written immediately
    /// on the calling thread. This guarantees ordering and delivery but may negatively impact
    /// engine performance, especially when logging to the console.
    /// </remarks>
    /// <value>
    /// The logging execution mode. The default value is <see cref="EngineLoggingMode.Asynchronous"/>.
    /// </value>
    public EngineLoggingMode LoggingMode { get; set; } = EngineLoggingMode.Asynchronous;

    private int _loggingQueueCapacity = 8192;

    /// <summary>
    /// Maximum number of queued log events when <see cref="LoggingMode"/> is
    /// <see cref="EngineLoggingMode.Asynchronous"/>.
    /// When full, log events are dropped (fire-and-forget).
    /// Default is 8192.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when attempting to set a value less than or equal to 0.
    /// </exception>
    public int LoggingQueueCapacity
    {
        get => _loggingQueueCapacity;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Capacity must be > 0.");
            _loggingQueueCapacity = value;
        }
    }

    /// <summary>
    /// If true, the engine will attempt to flush queued async log events during shutdown.
    /// Default is true.
    /// </summary>
    public bool FlushAsyncLogsOnShutdown { get; set; } = true;

    /// <summary>
    /// Optional collection of serialized <see cref="EngineState"/>s to mount at initialization.
    /// </summary>
    public List<StateFileMount>? StateFiles { get; set; }
}
