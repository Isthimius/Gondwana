using Microsoft.Extensions.Logging;

namespace Gondwana.Logging;

public static class GondwanaLogger
{
    private static ILoggerFactory _loggerFactory = LoggerFactory.Create(static builder =>
    {
        builder.AddDebug();
        builder.AddConsole(); // only visible in Console apps
    });

    internal static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public static ILoggerFactory EngineLoggerFactory => _loggerFactory;

    public static ILogger<T> GetLogger<T>() =>
        _loggerFactory.CreateLogger<T>();
}
