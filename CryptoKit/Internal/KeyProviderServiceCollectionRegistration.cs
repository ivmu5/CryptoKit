using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Internal;

/// <summary>
/// Contains shared dependency-injection registration logic for algorithm-specific key providers.
/// </summary>
internal static class KeyProviderServiceCollectionRegistration
{
    /// <summary>
    /// Registers a generator and a singleton provider that resolves the configured
    /// <see cref="IKeyStorage"/> from the service container.
    /// </summary>
    internal static IServiceCollection AddKeyProvider<TGenerator, TService, TOptions>(
        IServiceCollection services,
        TOptions options,
        Func<IKeyStorage, TGenerator, TOptions, TService> providerFactory)
        where TGenerator : class
        where TService : class
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(providerFactory);

        services.AddSingleton<TGenerator>();

        services.AddSingleton<TService>(serviceProvider =>
            providerFactory(
                serviceProvider.GetRequiredService<IKeyStorage>(),
                serviceProvider.GetRequiredService<TGenerator>(),
                options));

        return services;
    }
}
