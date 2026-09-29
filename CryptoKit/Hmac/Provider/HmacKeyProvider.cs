using CryptoKit.Internal;
using CryptoKit.Storage;

namespace CryptoKit.Hmac;

/// <summary>
/// Provides asynchronous access to HMAC keys backed by an <see cref="IKeyStorage"/>.
/// </summary>
public sealed class HmacKeyProvider : IHmacKeyProvider
{
    private const string KeySuffix = ".hmac";

    private readonly StoredSecretKeyProvider<HmacKey> _inner;

    /// <summary>
    /// Creates an HMAC key provider.
    /// </summary>
    /// <param name="storage">The storage used to persist HMAC key material.</param>
    /// <param name="generator">The generator used when a missing key must be created.</param>
    /// <param name="options">The generation options captured by this provider.</param>
    public HmacKeyProvider(
        IKeyStorage storage,
        HmacKeyGenerator generator,
        HmacKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);

        HmacKeyOptionsValidator.Validate(options);

        // Capture the value so later mutations of the supplied options object do not
        // change the behavior of this provider instance.
        var keySize = options.KeySize;

        _inner = new StoredSecretKeyProvider<HmacKey>(
            storage,
            static keyData => new HmacKey(keyData),
            () => generator.Generate(keySize),
            KeySuffix,
            "HMAC key");
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
