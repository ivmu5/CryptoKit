using CryptoKit.Internal;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Rsa;

/// <summary>
/// Provides dependency-injection registration for CryptoKit RSA services.
/// </summary>
public static class RsaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the RSA key generator and key provider.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">
    /// An optional callback used to configure newly generated RSA key pairs.
    /// </param>
    /// <returns>The original service collection.</returns>
    /// <remarks>
    /// An <see cref="IKeyStorage"/> implementation must be registered before the
    /// provider is resolved. A single provider can manage multiple logical RSA key pairs.
    /// </remarks>
    public static IServiceCollection AddCryptoKitRsa(
        this IServiceCollection services,
        Action<RsaKeyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuredOptions = new RsaKeyOptions();
        configure?.Invoke(configuredOptions);

        RsaKeyOptionsValidator.Validate(configuredOptions);

        // Capture an immutable snapshot so later mutation of the caller's options object
        // cannot alter the lifetime behavior of the registered singleton provider.
        var options = new RsaKeyOptions
        {
            KeySize = configuredOptions.KeySize
        };

        return KeyProviderServiceCollectionRegistration
            .AddKeyProvider<RsaKeyGenerator, IRsaKeyProvider, RsaKeyOptions>(
                services,
                options,
                static (storage, generator, providerOptions) =>
                    new RsaKeyProvider(
                        storage,
                        generator,
                        providerOptions));
    }
}
