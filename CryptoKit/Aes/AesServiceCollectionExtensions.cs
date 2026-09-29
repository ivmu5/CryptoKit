using CryptoKit.Internal;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Aes;

/// <summary>
/// Provides dependency-injection registration for CryptoKit AES services.
/// </summary>
public static class AesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AES key generator and key provider.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">
    /// An optional callback used to configure newly generated AES keys.
    /// </param>
    /// <returns>The original service collection.</returns>
    /// <remarks>
    /// An <see cref="IKeyStorage"/> implementation must be registered before the
    /// provider is resolved. A single provider can manage multiple logical AES keys.
    /// </remarks>
    public static IServiceCollection AddCryptoKitAes(
        this IServiceCollection services,
        Action<AesKeyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuredOptions = new AesKeyOptions();
        configure?.Invoke(configuredOptions);

        AesKeyOptionsValidator.Validate(configuredOptions);

        // Capture an immutable snapshot so later mutation of the caller's options object
        // cannot alter the lifetime behavior of the registered singleton provider.
        var options = new AesKeyOptions
        {
            KeySize = configuredOptions.KeySize
        };

        return KeyProviderServiceCollectionRegistration
            .AddKeyProvider<AesKeyGenerator, IAesKeyProvider, AesKeyOptions>(
                services,
                options,
                static (storage, generator, providerOptions) =>
                    new AesKeyProvider(
                        storage,
                        generator,
                        providerOptions));
    }
}
