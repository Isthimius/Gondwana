using System.Runtime.CompilerServices;
using Gondwana.Logging;
using Microsoft.Extensions.Logging;

namespace Gondwana.Blazor.Logging;

/// <summary>
/// Writes <see cref="Microsoft.Extensions.Logging"/> records to the browser developer console
/// without using the desktop-oriented Microsoft console logging provider.
/// </summary>
/// <remarks>
/// <para>
/// Browser-hosted Gondwana games normally route records through <see cref="EngineLogger"/>, so
/// asynchronous engine logging still performs queueing before this provider is invoked.
/// </para>
/// <para>
/// The implementation uses <see cref="Console.Out"/> and <see cref="Console.Error"/>. In
/// WebAssembly these streams are bridged by the .NET browser runtime to the browser developer
/// console without requiring explicit JavaScript interop.
/// </para>
/// </remarks>
public sealed class BrowserConsoleLoggerProvider : ILoggerProvider
{
    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new BrowserConsoleLogger(categoryName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private sealed class BrowserConsoleLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
                return;

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null)
                return;

            string eventSuffix = eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name)
                ? $" [{eventId.Id}:{eventId.Name ?? string.Empty}]"
                : string.Empty;

            string line = $"[{FormatLevel(logLevel)}] {categoryName}{eventSuffix}: {message}";
            if (exception is not null)
                line += Environment.NewLine + exception;

            TextWriter writer = logLevel >= LogLevel.Warning
                ? Console.Error
                : Console.Out;
            writer.WriteLine(line);
        }

        private static string FormatLevel(LogLevel level) => level switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "error",
            LogLevel.Critical => "critical",
            _ => level.ToString().ToLowerInvariant()
        };
    }
}

/// <summary>
/// Connects Gondwana's engine logger to the browser developer console.
/// </summary>
public static class BrowserConsoleLogging
{
    private static readonly ConditionalWeakTable<ILoggerFactory, object> AttachedFactories = new();
    private static readonly object SyncRoot = new();

    /// <summary>
    /// Adds a <see cref="BrowserConsoleLoggerProvider"/> to the active Gondwana logger factory.
    /// Repeated calls for the same factory are ignored.
    /// </summary>
    public static void AttachToEngineLogger()
    {
        ILoggerFactory factory = EngineLogger.EngineLoggerFactory;

        lock (SyncRoot)
        {
            if (AttachedFactories.TryGetValue(factory, out _))
                return;

            factory.AddProvider(new BrowserConsoleLoggerProvider());
            AttachedFactories.Add(factory, new object());
        }
    }
}
