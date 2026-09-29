using CryptoKit.Internal;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoKit.Rsa;

/// <summary>
/// Содержит методы расширения для регистрации RSA-компонентов CryptoKit.
/// </summary>
public static class RsaServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет генератор и провайдер RSA-ключей
    /// в коллекцию сервисов приложения.
    /// </summary>
    /// <param name="services">
    /// Коллекция сервисов приложения.
    /// </param>
    /// <param name="configure">
    /// Необязательная настройка параметров создаваемых RSA-ключей.
    /// </param>
    /// <returns>
    /// Исходная коллекция сервисов для возможности цепочного вызова методов.
    /// </returns>
    /// <remarks>
    /// Перед использованием RSA в контейнере должна быть зарегистрирована
    /// реализация <see cref="IKeyStorage"/>.
    ///
    /// Один зарегистрированный провайдер может работать
    /// с несколькими RSA-парами по разным идентификаторам.
    /// </remarks>
    public static IServiceCollection AddCryptoKitRsa(
        this IServiceCollection services,
        Action<RsaKeyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuredOptions = new RsaKeyOptions();
        configure?.Invoke(configuredOptions);

        RsaKeyOptionsValidator.Validate(configuredOptions);

        // Провайдер получает отдельный снимок настроек. Изменяемый объект,
        // переданный в configure, не сохраняется и не публикуется через DI.
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
