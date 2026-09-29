using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Storage;

/// <summary>
/// Provides dependency-injection registration for CryptoKit storage backends.
/// </summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileKeyStorage"/> as the application's <see cref="IKeyStorage"/>.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">
    /// The required configuration callback. The storage directory must be set explicitly.
    /// </param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddCryptoKitFileStorage(
        this IServiceCollection services,
        Action<FileKeyStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FileKeyStorageOptions();
        configure(options);

        FileKeyStorageOptionsValidator.Validate(options);

        var directoryPath = Path.GetFullPath(options.DirectoryPath);

        services.AddSingleton<IKeyStorage>(_ =>
            new FileKeyStorage(
                new FileKeyStorageOptions
                {
                    DirectoryPath = directoryPath,
                    MaximumEntrySizeBytes = options.MaximumEntrySizeBytes
                }));

        return services;
    }
}
