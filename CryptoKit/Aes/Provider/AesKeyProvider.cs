using CryptoKit.Internal;
using CryptoKit.Storage;

namespace CryptoKit.Aes;

/// <summary>
/// Предоставляет асинхронный доступ к AES-ключам через настроенное хранилище.
/// </summary>
public sealed class AesKeyProvider : IAesKeyProvider
{
    private const string KeySuffix = ".aes";

    private readonly StoredSecretKeyProvider<AesKey> _inner;

    /// <summary>
    /// Создаёт провайдер AES-ключей.
    /// </summary>
    /// <param name="storage">Асинхронное хранилище ключевого материала.</param>
    /// <param name="generator">Генератор новых AES-ключей.</param>
    /// <param name="options">Настройки создаваемых AES-ключей.</param>
    public AesKeyProvider(
        IKeyStorage storage,
        AesKeyGenerator generator,
        AesKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);

        AesKeyOptionsValidator.Validate(options);

        // Фиксируем значение настройки, чтобы последующее изменение
        // переданного AesKeyOptions не влияло на поведение провайдера.
        var keySize = options.KeySize;

        _inner = new StoredSecretKeyProvider<AesKey>(
            storage,
            static keyData => new AesKey(keyData),
            () => generator.Generate(keySize),
            KeySuffix,
            "AES-ключ");
    }

    /// <inheritdoc />
    public ValueTask<AesKey> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetAsync(
            keyId,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<AesKey> GetOrCreateKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetOrCreateAsync(
            keyId,
            cancellationToken);
    }
}
