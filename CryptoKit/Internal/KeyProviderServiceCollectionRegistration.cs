using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Internal;

internal static class KeyProviderServiceCollectionRegistration
{
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
