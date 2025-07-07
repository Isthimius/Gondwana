using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Gondwana.Logging;

public static class GondwanaLogger
{
    private static ILoggerFactory _loggerFactory = LoggerFactory.Create(static builder =>
    {
        builder.AddDebug();
        builder.AddConsole(); // only visible in Console apps
    });

    private static readonly ConcurrentDictionary<Type, ILogger> _loggerCache = new();

    internal static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _loggerCache.Clear(); // optional; clears cache if factory changes
    }

    public static ILoggerFactory EngineLoggerFactory => _loggerFactory;

    public static ILogger<T> GetLogger<T>() =>
        (ILogger<T>)_loggerCache.GetOrAdd(typeof(T), static type =>
        {
            var genericMethod = typeof(ILoggerFactory)
                .GetMethod(nameof(ILoggerFactory.CreateLogger), 1, Type.EmptyTypes)!
                .MakeGenericMethod(type);
            return (ILogger)genericMethod.Invoke(_loggerFactory, null)!;
        });
}
