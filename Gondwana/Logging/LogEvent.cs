using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static partial class EngineLogger
{
    private readonly struct LogEvent
    {
        public LogEvent(
            string categoryName,
            LogLevel logLevel,
            EventId eventId,
            object? state,
            Exception? exception,
            Func<object?, Exception?, string> formatter)
        {
            CategoryName = categoryName;
            LogLevel = logLevel;
            EventId = eventId;
            State = state;
            Exception = exception;
            Formatter = formatter;
        }

        public string CategoryName { get; }
        public LogLevel LogLevel { get; }
        public EventId EventId { get; }
        public object? State { get; }
        public Exception? Exception { get; }
        public Func<object?, Exception?, string> Formatter { get; }
    }
}
