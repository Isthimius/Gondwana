using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static partial class EngineLogger
{
    /// <summary>
    /// Internal logger wrapper that routes log messages through either synchronous or asynchronous paths
    /// based on the current <see cref="Mode"/> setting.
    /// Implements <see cref="ILogger{T}"/> to provide a type-specific logger interface.
    /// </summary>
    /// <typeparam name="T">The type associated with this logger instance. Used to derive the category name.</typeparam>
    /// <remarks>
    /// This class is cached per type by <see cref="GetLogger{T}"/> for performance.
    /// When the logger factory is changed via <see cref="Initialize"/> or <see cref="SetLogLevel"/>,
    /// the cache is cleared and new instances are created with the updated factory.
    /// </remarks>
    private sealed class ModeLogger<T> : ILogger<T>
    {
        private static readonly string CategoryName =
            typeof(T).FullName ?? typeof(T).Name;

        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModeLogger{T}"/> class.
        /// Creates an underlying logger instance using the current logger factory and the category name derived from type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// The underlying logger is created once per wrapper instance.
        /// When <see cref="EngineLogger"/> clears the logger cache (e.g., after factory changes),
        /// new instances will be created with the updated configuration.
        /// </remarks>
        public ModeLogger()
        {
            // Create once per wrapper instance. EngineLogger clears _loggerCache when factory changes.
            _logger = _loggerFactory.CreateLogger(CategoryName);
        }

        /// <summary>
        /// Begins a logical operation scope for grouping related log messages.
        /// </summary>
        /// <typeparam name="TState">The type of the state object that defines the scope.</typeparam>
        /// <param name="state">The state object that identifies the scope. Must not be null.</param>
        /// <returns>
        /// An <see cref="IDisposable"/> that ends the logical operation scope when disposed.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <strong>Important:</strong> Scopes do not reliably flow across async boundaries in asynchronous logging mode.
        /// </para>
        /// <list type="bullet">
        /// <item><description>In <see cref="EngineLoggingMode.Synchronous"/> mode: Scopes work normally and context is preserved.</description></item>
        /// <item><description>In <see cref="EngineLoggingMode.Asynchronous"/> mode: Scope context may be lost when log messages 
        /// are queued and processed on the background thread.</description></item>
        /// </list>
        /// </remarks>
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            // NOTE: Scopes don't reliably "flow" across async boundary.
            // They will work normally in sync mode; in async mode, scope context may be lost.
            return _logger.BeginScope(state);
        }

        /// <summary>
        /// Checks if logging is enabled for the specified log level.
        /// </summary>
        /// <param name="logLevel">The log level to check.</param>
        /// <returns>
        /// <c>true</c> if logging is enabled for the specified level; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method should be called before expensive logging operations to avoid unnecessary work
        /// when the log level is not enabled. The <see cref="Log{TState}"/> method internally performs
        /// this check, so explicit calls are only needed for optimization purposes.
        /// </remarks>
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        /// <summary>
        /// Writes a log entry with the specified parameters.
        /// Routes the log message through synchronous or asynchronous paths based on the current <see cref="Mode"/>.
        /// </summary>
        /// <typeparam name="TState">The type of the state object that contains the log data.</typeparam>
        /// <param name="logLevel">The severity level of the log entry.</param>
        /// <param name="eventId">The event identifier associated with the log entry.</param>
        /// <param name="state">The state object containing log message data and structured information.</param>
        /// <param name="exception">The exception associated with the log entry, or <c>null</c> if none.</param>
        /// <param name="formatter">A function that creates a log message string from the state and exception.</param>
        /// <remarks>
        /// <para>
        /// Behavior depends on the current <see cref="EngineLogger.Mode"/>:
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="EngineLoggingMode.Synchronous"/>: The log is written immediately on the calling thread.</description></item>
        /// <item><description><see cref="EngineLoggingMode.Asynchronous"/>: The log entry is queued to a bounded channel for background processing.
        /// If the channel is full, the message is dropped (fire-and-forget behavior) to prevent blocking the caller.</description></item>
        /// </list>
        /// <para>
        /// If the specified <paramref name="logLevel"/> is not enabled, the method returns immediately without processing.
        /// </para>
        /// </remarks>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!_logger.IsEnabled(logLevel))
                return;

            if (_mode == EngineLoggingMode.Synchronous)
            {
                _logger.Log(logLevel, eventId, state, exception, formatter);
                return;
            }

            // Async: queue and drop if full. No sync fallback.
            Func<object?, Exception?, string> boxedFormatter = (s, e) =>
                formatter((TState)s!, e);

            var ev = new LogEvent(CategoryName, logLevel, eventId, state, exception, boxedFormatter);

            _ = TryEnqueue(ev); // ignore false (drop)
        }
    }
}
