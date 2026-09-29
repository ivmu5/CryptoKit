using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Storage;

/// <summary>
/// Содержит методы расширения для регистрации хранилищ ключей CryptoKit.
/// </summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет файловое хранилище криптографических ключей.
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения.</param>
    /// <param name="configure">
    /// Обязательная настройка файлового хранилища.
    /// В частности, приложение должно явно задать каталог хранения ключей.
    /// </param>
    /// <returns>
    /// Исходная коллекция сервисов для возможности цепочного вызова методов.
    /// </returns>
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
