using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static partial class EngineLogger
{
    /// <summary>
    /// Represents a captured log entry that can be queued for asynchronous processing.
    /// Contains all the information needed to reconstruct and write a log message on a background thread.
    /// </summary>
    /// <remarks>
    /// This struct is immutable and designed to be efficiently enqueued to a channel for async logging.
    /// All formatting and state objects are captured at the time of logging to ensure thread-safety.
    /// </remarks>
    private readonly struct LogEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogEvent"/> struct with all required logging information.
        /// </summary>
        /// <param name="categoryName">The category name for the logger (typically the full type name).</param>
        /// <param name="logLevel">The severity level of the log entry.</param>
        /// <param name="eventId">The event identifier associated with the log entry.</param>
        /// <param name="state">The state object containing the log message and any structured data.</param>
        /// <param name="exception">The exception associated with the log entry, or <c>null</c> if none.</param>
        /// <param name="formatter">A function that formats the state and exception into a log message string.</param>
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

        /// <summary>
        /// Gets the category name for the logger, typically representing the source type of the log message.
        /// </summary>
        public string CategoryName { get; }
        
        /// <summary>
        /// Gets the severity level of this log entry (e.g., Information, Warning, Error).
        /// </summary>
        public LogLevel LogLevel { get; }
        
        /// <summary>
        /// Gets the event identifier associated with this log entry, used for filtering and categorization.
        /// </summary>
        public EventId EventId { get; }
        
        /// <summary>
        /// Gets the state object containing the log message data and any structured logging information.
        /// May be <c>null</c> if no state was provided.
        /// </summary>
        public object? State { get; }
        
        /// <summary>
        /// Gets the exception associated with this log entry, or <c>null</c> if no exception was logged.
        /// </summary>
        public Exception? Exception { get; }
        
        /// <summary>
        /// Gets the formatter function that converts the <see cref="State"/> and <see cref="Exception"/> 
        /// into a formatted log message string.
        /// </summary>
        public Func<object?, Exception?, string> Formatter { get; }
    }
}
