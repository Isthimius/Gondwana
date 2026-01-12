using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static partial class EngineLogger
{
    private sealed class ModeLogger<T> : ILogger<T>
    {
        private static readonly string CategoryName =
            typeof(T).FullName ?? typeof(T).Name;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            // NOTE: Scopes don't reliably "flow" across async boundary.
            // They will work normally in sync mode; in async mode, scope context may be lost.
            var logger = _loggerFactory.CreateLogger(CategoryName);
            return logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            var logger = _loggerFactory.CreateLogger(CategoryName);
            return logger.IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var logger = _loggerFactory.CreateLogger(CategoryName);
            if (!logger.IsEnabled(logLevel))
                return;

            if (_mode == EngineLoggingMode.Synchronous)
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
                return;
            }

            // Async: queue and drop if full. No sync fallback (per requirement).
            Func<object?, Exception?, string> boxedFormatter = (s, e) =>
                formatter((TState)s!, e);

            var ev = new LogEvent(CategoryName, logLevel, eventId, state, exception, boxedFormatter);

            _ = TryEnqueue(ev); // ignore false (drop)
        }
    }
}
