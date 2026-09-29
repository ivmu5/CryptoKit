using CryptoKit.Internal;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Aes;

/// <summary>
/// Содержит методы расширения для регистрации AES-компонентов CryptoKit.
/// </summary>
public static class AesServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет генератор и провайдер AES-ключей
    /// в коллекцию сервисов приложения.
    /// </summary>
    /// <param name="services">
    /// Коллекция сервисов приложения.
    /// </param>
    /// <param name="configure">
    /// Необязательная настройка параметров создаваемых AES-ключей.
    /// </param>
    /// <returns>
    /// Исходная коллекция сервисов для возможности цепочного вызова методов.
    /// </returns>
    /// <remarks>
    /// Перед использованием AES в контейнере должна быть зарегистрирована
    /// реализация <see cref="IKeyStorage"/>.
    ///
    /// Один зарегистрированный провайдер может работать
    /// с несколькими AES-ключами по разным идентификаторам.
    /// </remarks>
    public static IServiceCollection AddCryptoKitAes(
        this IServiceCollection services,
        Action<AesKeyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuredOptions = new AesKeyOptions();
        configure?.Invoke(configuredOptions);

        AesKeyOptionsValidator.Validate(configuredOptions);

        // Провайдер получает отдельный снимок настроек. Изменяемый объект,
        // переданный в configure, не сохраняется и не публикуется через DI.
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
