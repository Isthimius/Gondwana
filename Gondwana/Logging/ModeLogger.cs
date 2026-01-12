using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static partial class EngineLogger
{
    private sealed class ModeLogger<T> : ILogger<T>
    {
        private static readonly string CategoryName =
            typeof(T).FullName ?? typeof(T).Name;

        private readonly ILogger _logger;

        public ModeLogger()
        {
            // Create once per wrapper instance. EngineLogger clears _loggerCache when factory changes.
            _logger = _loggerFactory.CreateLogger(CategoryName);
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            // NOTE: Scopes don't reliably "flow" across async boundary.
            // They will work normally in sync mode; in async mode, scope context may be lost.
            return _logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

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
