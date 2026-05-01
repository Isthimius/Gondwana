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
    /// on all currently registered GPU surfaces to keep that value consistent.  It also serves as
    /// the default value for any new <see cref="GpuBackbuffer"/> instances created after this
    /// property is set.
    /// </remarks>
    public int TargetFPS
    {
        get => _targetFPS;
        set
        {
            _targetFPS = value < 0 ? 0 : value;

            // Propagate to all active GPU backbuffers so their TargetFps stays in sync.
            foreach (var surface in RenderSurfaceHostRegistry.All.ToArray())
            {
                if (surface.Backbuffer is GpuBackbuffer gpuBb)
                    gpuBb.TargetFps = _targetFPS;
            }
        }
    }

    private bool _vSync = true;

    /// <summary>
    /// Gets or sets a value indicating whether vertical synchronisation (vsync) is enabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This setting only applies to <see cref="GpuBackbuffer"/>; other backbuffer types ignore it.
    /// </para>
    /// <para>
    /// Setting this property propagates the value to
    /// <see cref="Gondwana.Rendering.Backbuffers.GpuBackbuffer.VSync"/> on all currently registered
    /// GPU surfaces.  It also serves as the default value for any new <see cref="GpuBackbuffer"/>
    /// instances created after this property is set.  The change is applied to each surface lazily
    /// at the start of its next <c>PaintSurface</c> callback.
    /// </para>
    /// <para>
    /// Enabling vsync prevents screen tearing but caps the frame rate to the monitor refresh rate.
    /// Disabling vsync allows higher frame rates at the cost of potential tearing.
    /// </para>
    /// </remarks>
    /// <value>
    /// <see langword="true"/> to synchronise presentation with the monitor refresh; otherwise
    /// <see langword="false"/>.  The default is <see langword="true"/>.
    /// </value>
    public bool VSync
    {
        get => _vSync;
        set
        {
            _vSync = value;

            // Propagate to all active GPU backbuffers so their VSync stays in sync.
            foreach (var surface in RenderSurfaceHostRegistry.All.ToArray())
            {
                if (surface.Backbuffer is GpuBackbuffer gpuBb)
                    gpuBb.VSync = _vSync;
            }
        }
    }

    private int _msaaSampleCount = 1;

    /// <summary>
    /// Gets or sets the number of MSAA (multisample anti-aliasing) samples used when creating
    /// GPU render-target surfaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This setting only applies to <see cref="GpuBackbuffer"/>; other backbuffer types ignore it.
    /// </para>
    /// <para>
    /// Setting this property propagates the value to
    /// <see cref="Gondwana.Rendering.Backbuffers.GpuBackbuffer.MsaaSampleCount"/> on all currently
    /// registered GPU surfaces.  It also serves as the default value for any new
    /// <see cref="GpuBackbuffer"/> instances created after this property is set.  Because the
    /// GPU render-target surface must be recreated to change the sample count, the new value takes
    /// effect the next time <see cref="GpuBackbuffer.Initialize"/> is called on each surface
    /// (e.g. on the next window resize).
    /// </para>
    /// <para>
    /// A value of <c>1</c> disables MSAA.  Common higher values are <c>2</c>, <c>4</c>, and
    /// <c>8</c>, subject to hardware and driver support.
    /// </para>
    /// </remarks>
    /// <value>
    /// The MSAA sample count.  Values less than <c>1</c> are clamped to <c>1</c>.  The default is <c>1</c>.
    /// </value>
    public int MsaaSampleCount
    {
        get => _msaaSampleCount;
        set
        {
            _msaaSampleCount = value < 1 ? 1 : value;

            // Propagate to all active GPU backbuffers so their MsaaSampleCount stays in sync.
            foreach (var surface in RenderSurfaceHostRegistry.All.ToArray())
            {
                if (surface.Backbuffer is GpuBackbuffer gpuBb)
                    gpuBb.MsaaSampleCount = _msaaSampleCount;
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
