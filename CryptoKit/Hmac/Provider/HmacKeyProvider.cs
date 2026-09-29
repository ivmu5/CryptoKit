using CryptoKit.Internal;
using CryptoKit.Storage;

namespace CryptoKit.Hmac;

/// <summary>
/// Предоставляет асинхронный доступ к HMAC-ключам через настроенное хранилище.
/// </summary>
public sealed class HmacKeyProvider : IHmacKeyProvider
{
    private const string KeySuffix = ".hmac";

    private readonly StoredSecretKeyProvider<HmacKey> _inner;

    /// <summary>
    /// Создаёт провайдер HMAC-ключей.
    /// </summary>
    /// <param name="storage">Асинхронное хранилище ключевого материала.</param>
    /// <param name="generator">Генератор новых HMAC-ключей.</param>
    /// <param name="options">Настройки создаваемых HMAC-ключей.</param>
    public HmacKeyProvider(
        IKeyStorage storage,
        HmacKeyGenerator generator,
        HmacKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);

        HmacKeyOptionsValidator.Validate(options);

        // Фиксируем значение настройки, чтобы последующее изменение
        // переданного HmacKeyOptions не влияло на поведение провайдера.
        var keySize = options.KeySize;

        _inner = new StoredSecretKeyProvider<HmacKey>(
            storage,
            static keyData => new HmacKey(keyData),
            () => generator.Generate(keySize),
            KeySuffix,
            "HMAC-ключ");
    }

    /// <inheritdoc />
    public ValueTask<HmacKey> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetAsync(
            keyId,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<HmacKey> GetOrCreateKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetOrCreateAsync(
            keyId,
            cancellationToken);
    }
}
