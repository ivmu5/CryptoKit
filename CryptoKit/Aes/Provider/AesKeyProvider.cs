using CryptoKit.Internal;
using CryptoKit.Storage;

namespace CryptoKit.Aes;

/// <summary>
/// Provides asynchronous access to AES keys backed by an <see cref="IKeyStorage"/>.
/// </summary>
public sealed class AesKeyProvider : IAesKeyProvider
{
    private const string KeySuffix = ".aes";

    private readonly StoredSecretKeyProvider<AesKey> _inner;

    /// <summary>
    /// Creates an AES key provider.
    /// </summary>
    /// <param name="storage">The storage used to persist AES key material.</param>
    /// <param name="generator">The generator used when a missing key must be created.</param>
    /// <param name="options">The generation options captured by this provider.</param>
    public AesKeyProvider(
        IKeyStorage storage,
        AesKeyGenerator generator,
        AesKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);

        AesKeyOptionsValidator.Validate(options);

        // Capture the value so later mutations of the supplied options object do not
        // change the behavior of this provider instance.
        var keySize = options.KeySize;

        _inner = new StoredSecretKeyProvider<AesKey>(
            storage,
            static keyData => new AesKey(keyData),
            () => generator.Generate(keySize),
            KeySuffix,
            "AES key");
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
