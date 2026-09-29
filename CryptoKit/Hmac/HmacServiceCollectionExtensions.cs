using CryptoKit.Internal;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Hmac;

/// <summary>
/// Содержит методы расширения для регистрации HMAC-компонентов CryptoKit.
/// </summary>
public static class HmacServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет генератор и провайдер HMAC-ключей
    /// в коллекцию сервисов приложения.
    /// </summary>
    /// <param name="services">
    /// Коллекция сервисов приложения.
    /// </param>
    /// <param name="configure">
    /// Необязательная настройка параметров создаваемых HMAC-ключей.
    /// </param>
    /// <returns>
    /// Исходная коллекция сервисов для возможности цепочного вызова методов.
    /// </returns>
    /// <remarks>
    /// Перед использованием HMAC в контейнере должна быть зарегистрирована
    /// реализация <see cref="IKeyStorage"/>.
    ///
    /// Один зарегистрированный провайдер может работать
    /// с несколькими HMAC-ключами по разным идентификаторам.
    /// </remarks>
    public static IServiceCollection AddCryptoKitHmac(
        this IServiceCollection services,
        Action<HmacKeyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuredOptions = new HmacKeyOptions();
        configure?.Invoke(configuredOptions);

        HmacKeyOptionsValidator.Validate(configuredOptions);

        // Провайдер получает отдельный снимок настроек. Изменяемый объект,
        // переданный в configure, не сохраняется и не публикуется через DI.
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
