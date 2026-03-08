using Gondwana.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gondwana.Extensibility;

/// <summary>
/// Provides extension methods for configuring Gondwana services in an <see cref="IServiceCollection"/>.
/// </summary>
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