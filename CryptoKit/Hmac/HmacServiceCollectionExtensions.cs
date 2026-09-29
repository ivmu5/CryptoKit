using CryptoKit.Internal;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Hmac;

/// <summary>
/// Provides dependency-injection registration for CryptoKit HMAC services.
/// </summary>
public static class HmacServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HMAC key generator and key provider.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">
    /// An optional callback used to configure newly generated HMAC keys.
    /// </param>
    /// <returns>The original service collection.</returns>
    /// <remarks>
    /// An <see cref="IKeyStorage"/> implementation must be registered before the
    /// provider is resolved. A single provider can manage multiple logical HMAC keys.
    /// </remarks>
    public static IServiceCollection AddCryptoKitHmac(
        this IServiceCollection services,
        Action<HmacKeyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuredOptions = new HmacKeyOptions();
        configure?.Invoke(configuredOptions);

        HmacKeyOptionsValidator.Validate(configuredOptions);

        // Capture an immutable snapshot so later mutation of the caller's options object
        // cannot alter the lifetime behavior of the registered singleton provider.
        var options = new HmacKeyOptions
        {
            KeySize = configuredOptions.KeySize
        };

        return KeyProviderServiceCollectionRegistration
            .AddKeyProvider<HmacKeyGenerator, IHmacKeyProvider, HmacKeyOptions>(
                services,
                options,
                static (storage, generator, providerOptions) =>
                    new HmacKeyProvider(
                        storage,
                        generator,
                        providerOptions));
    }
}
