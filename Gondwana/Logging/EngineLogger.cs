using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static partial class EngineLogger
{
    private static ILoggerFactory _loggerFactory = LoggerFactory.Create(static builder =>
    {
        builder.AddDebug()
               .AddConsole(); // only visible in Console apps
    });

    // Cache wrappers (not raw loggers)
    private static readonly ConcurrentDictionary<Type, ILogger> _loggerCache = new();

    // Mode (default async)
    private static volatile EngineLoggingMode _mode = EngineLoggingMode.Asynchronous;

    public static EngineLoggingMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;

            // If switching to async, ensure worker exists.
            if (_mode == EngineLoggingMode.Asynchronous)
                EnsureAsyncStarted();
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
    /// Optionally call during startup. If not called and Mode==Asynchronous, the worker auto-starts on first log.
    /// </summary>
    public static void StartAsyncLogging(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be > 0.");

        _capacity = capacity;
        EnsureAsyncStarted(forceRestart: false);
    }

    /// <summary>
    /// Stops background logging. If flush=true, tries to drain queued messages first.
    /// </summary>
    public static void StopAsyncLogging(bool flush = true, TimeSpan? flushTimeout = null)
    {
        Channel<LogEvent>? ch;
        Task? worker;

        lock (_asyncGate)
        {
            ch = _channel;
            worker = _worker;

            if (ch == null || worker == null)
                return;

            // Completing the writer lets the worker drain then exit.
            ch.Writer.TryComplete();
        }

        if (flush)
        {
            var timeout = flushTimeout ?? TimeSpan.FromSeconds(2);
            try { worker.Wait(timeout); } catch { /* never crash */ }
        }

        lock (_asyncGate)
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            _cts?.Dispose();

            _cts = null;
            _worker = null;
            _channel = null;
        }
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
        if (capacity is not null)
        {
            if (capacity.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be > 0.");
            _capacity = capacity.Value;
        }

        _mode = EngineLoggingMode.Asynchronous;
        EnsureAsyncStarted(forceRestart: false);
    }

    internal static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _loggerCache.Clear(); // refresh wrappers

        // Worker continues; it uses _loggerFactory dynamically.
    }

    public static ILoggerFactory EngineLoggerFactory => _loggerFactory;

    public static ILogger<T> GetLogger<T>() =>
        (ILogger<T>)_loggerCache.GetOrAdd(typeof(T), static _ => new ModeLogger<T>());

    public static void SetLogLevel(LogLevel level)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddDebug()
                   .AddConsole()
                   .SetMinimumLevel(level);
        });

        _loggerCache.Clear(); // refresh wrappers
    }

    private static void EnsureAsyncStarted(bool forceRestart = false)
    {
        lock (_asyncGate)
        {
            if (_mode != EngineLoggingMode.Asynchronous)
                return;
            if (!forceRestart && _worker != null)
                return;

            if (forceRestart && _worker != null)
            {
                // best-effort stop without flush; caller controls flush via StopAsyncLogging.
                try { _channel?.Writer.TryComplete(); } catch { /* ignore */ }
            }

            _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(_capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite // <-- your requirement
            });

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => WorkerLoop(_cts.Token));
        }
    }

    private static bool TryEnqueue(in LogEvent ev)
    {
        if (_mode != EngineLoggingMode.Asynchronous)
            return false;

        EnsureAsyncStarted(forceRestart: false);

        var ch = _channel;
        if (ch == null)
            return false;

        // Fire-and-forget: never block, drop if full
        return ch.Writer.TryWrite(ev);
    }

    private static async Task WorkerLoop(CancellationToken ct)
    {
        var reader = _channel!.Reader;

        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out var ev))
                {
                    try
                    {
                        // Executes formatting + sinks (console) off the engine thread.
                        var logger = _loggerFactory.CreateLogger(ev.CategoryName);
                        logger.Log(ev.LogLevel, ev.EventId, ev.State, ev.Exception, ev.Formatter);
                    }
                    catch
                    {
                        // Never let logging crash the engine.
                    }
                }
            }
        }
        catch
        {
            // Swallow cancellation and any channel exceptions.
        }
    }
}
