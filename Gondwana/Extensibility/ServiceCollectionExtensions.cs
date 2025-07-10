using Gondwana.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gondwana.Extensibility;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ILogger specified by DI
    /// </summary>
    /// <returns>recursive to this IServiceCollection for chaining</returns>
    public static IServiceCollection AddEngineLogging(this IServiceCollection services)
    {
        services.AddLogging(); // just in case it's not already registered
        services.AddSingleton(provider =>
        {
            var factory = provider.GetRequiredService<ILoggerFactory>();
            EngineLogger.Initialize(factory);
            return EngineLogger.EngineLoggerFactory;
        });

        return services;
    }
}