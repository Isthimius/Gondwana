using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

/// <summary>
/// Provides centralized logging infrastructure for the Gondwana engine with support for both 
/// synchronous and asynchronous logging modes.
/// </summary>
/// <remarks>
/// <para>
/// This static class manages logging across the engine, supporting two distinct modes:
/// <list type="bullet">
/// <item><description><see cref="EngineLoggingMode.Asynchronous"/> (default): Log messages are queued to a bounded channel 
/// and processed by a background worker thread, preventing logging operations from blocking the engine thread.</description></item>
/// <item><description><see cref="EngineLoggingMode.Synchronous"/>: Log messages are written directly on the calling thread, 
/// useful for debugging or when deterministic ordering is required.</description></item>
/// </list>
/// </para>
/// <para>
/// The asynchronous mode uses a fire-and-forget strategy with a bounded channel (default capacity: 8192). 
/// When the channel is full, new log messages are dropped rather than blocking the caller. This design prioritizes 
/// engine performance over guaranteed log delivery.
/// </para>
/// <para>
/// Logger instances are cached per type for performance. The underlying <see cref="ILoggerFactory"/> can be 
/// customized via <see cref="Initialize"/> or <see cref="SetLogLevel"/>. By default, Debug and Console 
/// providers are configured on non-browser platforms. On browser/WASM platforms, only Debug logging is used
/// to avoid threading issues.
/// </para>
/// <para>
/// Thread-safety: All public methods and properties are thread-safe. The <see cref="LoggingError"/> event 
/// is raised when exceptions occur during logging operations, allowing applications to monitor logging health.
/// </para>
/// </remarks>
public static partial class EngineLogger
{
    private static ILoggerFactory? _loggerFactory;
    private static bool _usingExternalLoggerFactory;
    private static readonly object _factoryLock = new();

    // Cache wrappers (not raw loggers)
    private static readonly ConcurrentDictionary<Type, ILogger> _loggerCache = new();

    // Mode (default async)
    private static volatile EngineLoggingMode _mode = EngineLoggingMode.Asynchronous;

    /// <summary>
    /// Raised when an exception occurs in the logging infrastructure.
    /// Allows applications to monitor logging failures for diagnostics.
    /// </summary>
    public static event EventHandler<LoggingErrorEventArgs>? LoggingError;

    /// <summary>
    /// Gets or sets the current logging mode (synchronous or asynchronous).
    /// When set to <see cref="EngineLoggingMode.Asynchronous"/>, the background worker is automatically started.
    /// When set to <see cref="EngineLoggingMode.Synchronous"/>, logs are written directly on the calling thread.
    /// </summary>
    /// <remarks>
    /// Default mode is <see cref="EngineLoggingMode.Asynchronous"/>.
    /// Changing this property is thread-safe.
    /// </remarks>
    public static EngineLoggingMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;

            // If switching to async, ensure worker exists.
            // EnsureAsyncStarted is idempotent and safely handles repeated calls.
            if (_mode == EngineLoggingMode.Asynchronous)
                EnsureAsyncStarted(forceRestart: false);
        }
    }

    // Async pipeline
    private static readonly object _asyncGate = new();
    private static Channel<LogEvent>? _channel;
    private static Task? _worker;
    private static CancellationTokenSource? _cts;

    // Defaults: bounded + drop on full (fire-and-forget)
    private const int DefaultCapacity = 8192;
    private static int _capacity = DefaultCapacity;

    /// <summary>
    /// Gets the logger factory, creating it lazily if needed.
    /// On browser/WASM platforms, only Debug logging is used (no Console logger).
    /// On other platforms, both Debug and Console logging are configured.
    /// </summary>
    private static ILoggerFactory GetOrCreateLoggerFactory()
    {
        if (_loggerFactory != null)
            return _loggerFactory;

        lock (_factoryLock)
        {
            if (_loggerFactory != null)
                return _loggerFactory;

            // Check if we're running in a browser/WASM environment
            bool isBrowser = OperatingSystem.IsBrowser();

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();

                // Only add Console logger on non-browser platforms
                // Console logging creates background threads which are not supported in WASM
                if (!isBrowser)
                {
                    builder.AddConsole();
                }
            });

            return _loggerFactory;
        }
    }

    /// <summary>
    /// Optionally call during startup. If not called and Mode==Asynchronous, the worker auto-starts on first log.
    /// </summary>
    public static void StartAsyncLogging(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be > 0.");

        lock (_asyncGate)
        {
            _capacity = capacity;
        }

        EnsureAsyncStarted(forceRestart: false);
    }

    /// <summary>
    /// Stops background logging. If flush=true, tries to drain queued messages first.
    /// </summary>
    public static void StopAsyncLogging(bool flush = true, TimeSpan? flushTimeout = null)
    {
        Channel<LogEvent>? ch;
        Task? worker;
        CancellationTokenSource? cts;

        lock (_asyncGate)
        {
            ch = _channel;
            worker = _worker;
            cts = _cts;

            if (ch == null || worker == null)
                return;

            // Completing the writer lets the worker drain then exit.
            try { ch.Writer.TryComplete(); } catch { /* ignore */ }

            // Clear shared references so other threads can't enqueue on this instance.
            _channel = null;
            _worker = null;
            _cts = null;
        }

        try { cts?.Cancel(); } catch { /* never crash */ }

        if (flush)
        {
            var timeout = flushTimeout ?? TimeSpan.FromSeconds(2);
            try { worker!.Wait(timeout); } catch { /* never crash */ }
        }

        try { cts?.Dispose(); } catch { /* ignore */ }
    }

    /// <summary>
    /// Convenience: switches to sync mode and flushes any queued async logs first (best-effort).
    /// </summary>
    public static void SwitchToSyncAndFlush(TimeSpan? flushTimeout = null)
    {
        if (_mode == EngineLoggingMode.Asynchronous)
            StopAsyncLogging(flush: true, flushTimeout: flushTimeout);

        _mode = EngineLoggingMode.Synchronous;
    }

    /// <summary>
    /// Convenience: switches to async mode (auto-starts worker).
    /// </summary>
    public static void SwitchToAsync(int? capacity = null)
    {
        lock (_asyncGate)
        {
            if (capacity is not null)
            {
                if (capacity.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be > 0.");
                _capacity = capacity.Value;
            }

            _mode = EngineLoggingMode.Asynchronous;
        }

        EnsureAsyncStarted(forceRestart: false);
    }

    internal static void Initialize(ILoggerFactory loggerFactory)
    {
        lock (_factoryLock)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _usingExternalLoggerFactory = true;
            _loggerCache.Clear(); // refresh wrappers
        }
    }

    /// <summary>
    /// Gets the underlying <see cref="ILoggerFactory"/> used by the engine logger.
    /// This factory can be used to create additional loggers or configure advanced logging scenarios.
    /// </summary>
    /// <remarks>
    /// Changes to this factory (via <see cref="Initialize"/> or <see cref="SetLogLevel"/>) 
    /// will affect all subsequently created loggers.
    /// </remarks>
    public static ILoggerFactory EngineLoggerFactory => GetOrCreateLoggerFactory();

    /// <summary>
    /// Gets a logger instance for the specified type.
    /// The logger is cached and respects the current <see cref="Mode"/> setting.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for. The type name will be used as the logger category.</typeparam>
    /// <returns>
    /// An <see cref="ILogger{T}"/> instance that routes log messages according to the current logging mode
    /// (synchronous or asynchronous).
    /// </returns>
    /// <remarks>
    /// This method is thread-safe and returns cached instances for performance.
    /// The returned logger automatically adapts to changes in the <see cref="Mode"/> property.
    /// </remarks>
    public static ILogger<T> GetLogger<T>() =>
        (ILogger<T>)_loggerCache.GetOrAdd(typeof(T), static _ => new ModeLogger<T>());

    /// <summary>
    /// Sets the minimum log level for all loggers created by the engine logger factory.
    /// Recreates the internal logger factory with Debug and Console providers at the specified level.
    /// On browser/WASM platforms, only Debug logging is configured (no Console logger).
    /// </summary>
    /// <param name="level">The minimum <see cref="LogLevel"/> to log. Messages below this level will be filtered out.</param>
    /// <remarks>
    /// This method clears the logger cache, so any previously retrieved loggers may need to be refreshed.
    /// The new log level applies to all loggers created after this method is called.
    /// When an external factory has been supplied via <see cref="Initialize"/>, provider and filtering
    /// configuration remain owned by that factory and this method leaves it unchanged.
    /// </remarks>
    public static void SetLogLevel(LogLevel level)
    {
        lock (_factoryLock)
        {
            if (_usingExternalLoggerFactory)
                return;

            // Check if we're running in a browser/WASM environment
            bool isBrowser = OperatingSystem.IsBrowser();

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug()
                       .SetMinimumLevel(level);

                // Only add Console logger on non-browser platforms
                if (!isBrowser)
                {
                    builder.AddConsole();
                }
            });

            _loggerCache.Clear(); // refresh wrappers
        }
    }

    private static void EnsureAsyncStarted(bool forceRestart)
    {
        lock (_asyncGate)
        {
            EnsureAsyncStarted_NoLock(forceRestart);
        }
    }

    // Must be called under _asyncGate.
    private static void EnsureAsyncStarted_NoLock(bool forceRestart)
    {
        if (_mode != EngineLoggingMode.Asynchronous)
            return;

        if (!forceRestart && _worker != null)
            return;

        if (forceRestart && _channel != null)
        {
            // Best-effort stop without flush; caller controls flush via StopAsyncLogging.
            try { _channel.Writer.TryComplete(); } catch { /* ignore */ }
        }

        int capacityLocal = _capacity;

        var channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(capacityLocal)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite // fire-and-forget; drop if full
        });

        _channel = channel;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // Pass reader so WorkerLoop never dereferences static _channel (avoids races with Stop).
        var reader = channel.Reader;
        var token = _cts.Token;
        _worker = Task.Run(() => WorkerLoop(reader, token));
    }

    private static bool TryEnqueue(in LogEvent ev)
    {
        lock (_asyncGate)
        {
            if (_mode != EngineLoggingMode.Asynchronous)
                return false;

            EnsureAsyncStarted_NoLock(forceRestart: false);

            var ch = _channel;
            if (ch == null)
                return false;

            // Fire-and-forget: never block, drop if full
            return ch.Writer.TryWrite(ev);
        }
    }

    private static async Task WorkerLoop(ChannelReader<LogEvent> reader, CancellationToken ct)
    {
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out var ev))
                {
                    try
                    {
                        // Executes formatting + sinks (console) off the engine thread.
                        var logger = GetOrCreateLoggerFactory().CreateLogger(ev.CategoryName);
                        logger.Log(ev.LogLevel, ev.EventId, ev.State, ev.Exception, ev.Formatter);
                    }
                    catch (Exception ex)
                    {
                        // Fallback: write to debug output to aid diagnostics without crashing.
                        Debug.WriteLine($"[EngineLogger] Logging failed: {ex.GetType().Name}: {ex.Message}");

                        // Raise event for applications that want to monitor logging failures.
                        try
                        {
                            LoggingError?.Invoke(null, new LoggingErrorEventArgs(ex, ev.CategoryName, ev.LogLevel));
                        }
                        catch
                        {
                            // Never let event handlers crash the logging infrastructure.
                        }
                    }
                }
            }
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal cancellation: no action required.
        }
        catch
        {
            // Swallow any channel or unexpected exceptions.
        }
    }
}

/// <summary>
/// Provides data for the LoggingError event.
/// </summary>
public sealed class LoggingErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the exception that occurred during logging.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the category name of the logger that failed.
    /// </summary>
    public string CategoryName { get; }

    /// <summary>
    /// Gets the log level of the message that failed to log.
    /// </summary>
    public LogLevel LogLevel { get; }

    internal LoggingErrorEventArgs(Exception exception, string categoryName, LogLevel logLevel)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        CategoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        LogLevel = logLevel;
    }
}
