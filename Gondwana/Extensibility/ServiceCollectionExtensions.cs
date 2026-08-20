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
    /// Configures Gondwana to use the <see cref="ILoggerFactory"/> registered
    /// with the application's dependency-injection container.
    /// </summary>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddEngineLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure the standard Microsoft logging services exist.
        services.AddLogging();

        // Capture the ILoggerFactory registration that DI would normally resolve.
        // We cannot resolve ILoggerFactory from inside another ILoggerFactory
        // registration because that creates a circular dependency.
        var factoryDescriptor = services.Last(descriptor =>
            descriptor.ServiceType == typeof(ILoggerFactory)
            && !descriptor.IsKeyedService);

        // If the application supplied an ILoggerFactory instance directly,
        // there is nothing to defer: initialize Gondwana with that same instance
        // and leave DI ownership/registration unchanged.
        if (factoryDescriptor.ImplementationInstance is ILoggerFactory existingFactory)
        {
            EngineLogger.Initialize(existingFactory);
            return services;
        }

        // Replace the effective registration with a wrapper that constructs the
        // original factory exactly as DI would have, then exposes that same factory
        // to Gondwana.
        services.Remove(factoryDescriptor);

        services.Add(ServiceDescriptor.Describe(
            typeof(ILoggerFactory),
            provider =>
            {
                var factory = CreateLoggerFactory(provider, factoryDescriptor);
                EngineLogger.Initialize(factory);

                return factory;
            },
            factoryDescriptor.Lifetime));

        return services;
    }

    private static ILoggerFactory CreateLoggerFactory(
        IServiceProvider provider,
        ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationFactory is not null)
        {
            return (ILoggerFactory)descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return (ILoggerFactory)ActivatorUtilities.CreateInstance(
                provider,
                descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            "The registered ILoggerFactory service could not be constructed.");
    }
}
