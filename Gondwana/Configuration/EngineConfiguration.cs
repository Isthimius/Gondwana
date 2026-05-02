using Gondwana.Logging;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Timers;
using Newtonsoft.Json;

namespace Gondwana.Configuration;

/// <summary>
/// Settings used by the engine when cycling
/// </summary>
[JsonObject]
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
            foreach (var surface in RenderSurfaceHostRegistry.Snapshot())
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
            foreach (var surface in RenderSurfaceHostRegistry.Snapshot())
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
    /// <c>8</c>, subject to hardware and driver support.  If the requested sample count is not
    /// supported, <see cref="GpuBackbuffer.Initialize"/> automatically falls back to <c>1</c>
    /// so the surface is always valid.
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
            foreach (var surface in RenderSurfaceHostRegistry.Snapshot())
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

    #region ConfigurationSections

    /// <summary>
    /// Gets or sets a collection of structured configuration values organized into named sections.
    /// <para>
    /// Each top-level key represents a logical configuration section (for example, <c>"graphics"</c>, 
    /// <c>"audio"</c>, or <c>"input"</c>). Each section contains a set of string-based key/value pairs 
    /// that define configurable aspects of the engine or application.
    /// </para>
    /// <para>
    /// This property is intended for persistent configuration data such as user preferences, engine 
    /// tuning parameters, or feature flags. It is fully serialized and restored as part of the 
    /// <see cref="EngineConfiguration"/> lifecycle.
    /// </para>
    /// <para>
    /// Unlike runtime state containers (such as <c>EngineState</c> or <c>ValueBag</c>), values stored here 
    /// are expected to be stable, portable, and environment-agnostic. They should not represent transient 
    /// gameplay state or frequently mutating runtime data.
    /// </para>
    /// <para>
    /// The structure is intentionally string-based to ensure compatibility with serialization formats 
    /// and external tooling, while still allowing flexible organization without requiring changes to 
    /// the core configuration schema.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Set values
    /// engineConfiguration.ConfigurationSections["audio"]["volume"] = "0.8";
    /// engineConfiguration.ConfigurationSections["graphics"]["resolution"] = "1920x1080";
    ///
    /// // Read values
    /// var volume = engineConfiguration.ConfigurationSections["audio"]["volume"];
    /// var resolution = engineConfiguration.ConfigurationSections["graphics"]["resolution"];
    /// </code>
    /// </example>
    [JsonProperty]
    public Dictionary<string, Dictionary<string, string>> ConfigurationSections { get; set; } = new();

    /// <summary>
    /// Determines whether the specified configuration section exists.
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <returns>
    /// <see langword="true"/> if the section exists; otherwise, <see langword="false"/>.
    /// </returns>
    public bool HasConfigurationSection(string section)
    {
        return ConfigurationSections.ContainsKey(section);
    }

    /// <summary>
    /// Creates a configuration section if it does not already exist.
    /// </summary>
    /// <param name="section">The name of the configuration section to create.</param>
    /// <returns>
    /// The existing or newly created dictionary for the specified section.
    /// </returns>
    public Dictionary<string, string> CreateConfigurationSection(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        if (!ConfigurationSections.TryGetValue(section, out var values))
        {
            values = new Dictionary<string, string>();
            ConfigurationSections[section] = values;
        }

        return values;
    }

    /// <summary>
    /// Removes the specified configuration section and all key/value pairs contained within it.
    /// </summary>
    /// <param name="section">The name of the configuration section to remove.</param>
    /// <returns>
    /// <see langword="true"/> if the section was removed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool RemoveConfigurationSection(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        return ConfigurationSections.Remove(section);
    }

    /// <summary>
    /// Gets a read-only view of all key/value pairs in the specified configuration section.
    /// </summary>
    /// <param name="section">The name of the configuration section to retrieve.</param>
    /// <returns>
    /// A read-only dictionary containing the section values, or <see langword="null"/> if the section does not exist.
    /// </returns>
    public IReadOnlyDictionary<string, string>? GetConfigurationSection(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        return ConfigurationSections.TryGetValue(section, out var values)
            ? values
            : null;
    }

    /// <summary>
    /// Determines whether a configuration value exists in the specified section.
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <param name="key">The configuration key to check.</param>
    /// <returns>
    /// <see langword="true"/> if the section and key exist; otherwise, <see langword="false"/>.
    /// </returns>
    public bool HasConfigurationValue(string section, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ConfigurationSections.TryGetValue(section, out var values)
            && values.ContainsKey(key);
    }

    /// <summary>
    /// Gets a configuration value from the specified section.
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <param name="key">The configuration key to retrieve.</param>
    /// <param name="defaultValue">The value to return if the section or key does not exist.</param>
    /// <returns>
    /// The stored configuration value, or <paramref name="defaultValue"/> if no value exists.
    /// </returns>
    public string? GetConfigurationValue(string section, string key, string? defaultValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ConfigurationSections.TryGetValue(section, out var values)
            && values.TryGetValue(key, out var value)
                ? value
                : defaultValue;
    }

    /// <summary>
    /// Sets a configuration value in the specified section, creating the section if necessary.
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The string value to store.</param>
    public void SetConfigurationValue(string section, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var values = CreateConfigurationSection(section);
        values[key] = value;
    }

    /// <summary>
    /// Removes a configuration value from the specified section.
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <param name="key">The configuration key to remove.</param>
    /// <returns>
    /// <see langword="true"/> if the value was removed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool RemoveConfigurationValue(string section, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ConfigurationSections.TryGetValue(section, out var values)
            && values.Remove(key);
    }

    /// <summary>
    /// Clears all key/value pairs from the specified configuration section without removing the section itself.
    /// </summary>
    /// <param name="section">The name of the configuration section to clear.</param>
    /// <returns>
    /// <see langword="true"/> if the section existed and was cleared; otherwise, <see langword="false"/>.
    /// </returns>
    public bool ClearConfigurationSection(string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        if (!ConfigurationSections.TryGetValue(section, out var values))
            return false;

        values.Clear();
        return true;
    }

    /// <summary>
    /// Removes all configuration sections and all contained key/value pairs.
    /// </summary>
    public void ClearConfigurationSections()
    {
        ConfigurationSections.Clear();
    }

    /// <summary>
    /// Gets or sets a configuration section by name.
    /// <para>
    /// When getting, returns the existing section if present; otherwise <see langword="null"/>.
    /// When setting, replaces the entire section with the provided dictionary.
    /// </para>
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <returns>
    /// The dictionary of key/value pairs for the section, or <see langword="null"/> if the section does not exist.
    /// </returns>
    public Dictionary<string, string>? this[string section]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section);

            return ConfigurationSections.TryGetValue(section, out var values)
                ? values
                : null;
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section);

            if (value == null)
            {
                ConfigurationSections.Remove(section);
            }
            else
            {
                ConfigurationSections[section] = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a configuration value using section and key.
    /// <para>
    /// When getting, returns the value if found; otherwise <see langword="null"/>.
    /// When setting, creates the section if necessary and stores the value.
    /// </para>
    /// </summary>
    /// <param name="section">The name of the configuration section.</param>
    /// <param name="key">The configuration key within the section.</param>
    /// <returns>
    /// The stored configuration value, or <see langword="null"/> if not found.
    /// </returns>
    public string? this[string section, string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return ConfigurationSections.TryGetValue(section, out var values)
                && values.TryGetValue(key, out var value)
                    ? value
                    : null;
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (value == null)
            {
                if (ConfigurationSections.TryGetValue(section, out var values))
                {
                    values.Remove(key);
                }
                return;
            }

            if (!ConfigurationSections.TryGetValue(section, out var sectionValues))
            {
                sectionValues = new Dictionary<string, string>();
                ConfigurationSections[section] = sectionValues;
            }

            sectionValues[key] = value;
        }
    }

    #endregion ConfigurationSections
}
