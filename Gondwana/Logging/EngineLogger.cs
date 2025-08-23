using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Gondwana.Logging;

public static class EngineLogger
{
    private static ILoggerFactory _loggerFactory = LoggerFactory.Create(static builder =>
        {
            builder.AddDebug()
                   .AddConsole(); // only visible in Console apps
        });

    private static readonly ConcurrentDictionary<Type, ILogger> _loggerCache = new();

    internal static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _loggerCache.Clear(); // optional; clears cache if factory changes
    }

    public static ILoggerFactory EngineLoggerFactory => _loggerFactory;

    public static ILogger<T> GetLogger<T>() =>
        (ILogger<T>)_loggerCache.GetOrAdd(typeof(T), _ => _loggerFactory.CreateLogger<T>());

    public static void SetLogLevel(LogLevel level)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug()
                       .AddConsole()
                       .SetMinimumLevel(level);
            });

        _loggerCache.Clear(); // refresh cached loggers so new filter applies
    }
}
