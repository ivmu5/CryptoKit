using System.Security.Cryptography;
using CryptoKit.Internal;
using CryptoKit.Storage;

namespace CryptoKit.Rsa;

/// <summary>
/// Provides asynchronous access to persisted RSA key pairs through an
/// <see cref="IKeyStorage"/> backend.
/// </summary>
/// <remarks>
/// Only the private key is persisted, in PKCS#8 format. Public keys are treated as
/// derived material and are reconstructed from the stored private key when requested.
/// </remarks>
public sealed class RsaKeyProvider : IRsaKeyProvider
{
    private const string PrivateKeySuffix = ".private";

    private readonly IKeyStorage _storage;
    private readonly RsaKeyGenerator _generator;
    private readonly int _keySize;

    private readonly KeyedLock _keyLocks =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Creates an RSA key provider.
    /// </summary>
    /// <param name="storage">The backend used to persist private-key material.</param>
    /// <param name="generator">The generator used when a missing RSA pair must be created.</param>
    /// <param name="options">The generation options captured by this provider.</param>
    public RsaKeyProvider(
        IKeyStorage storage,
        RsaKeyGenerator generator,
        RsaKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);

        RsaKeyOptionsValidator.Validate(options);

        _storage = storage;
        _generator = generator;
        _keySize = options.KeySize;
    }

    /// <inheritdoc />
    public async ValueTask<RsaKeyPair> GetKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        using var keyLock = await _keyLocks
            .AcquireAsync(keyId, cancellationToken)
            .ConfigureAwait(false);

        return await LoadExistingKeyPairAsync(
                keyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<RsaKeyPair> GetOrCreateKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        using var keyLock = await _keyLocks
            .AcquireAsync(keyId, cancellationToken)
            .ConfigureAwait(false);

        return await LoadOrCreateKeyPairAsync(
                keyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> GetPublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        using var keyLock = await _keyLocks
            .AcquireAsync(keyId, cancellationToken)
            .ConfigureAwait(false);

        return await LoadExistingPublicKeyAsync(
                keyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> GetOrCreatePublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        using var keyLock = await _keyLocks
            .AcquireAsync(keyId, cancellationToken)
            .ConfigureAwait(false);

        return await LoadOrCreatePublicKeyAsync(
                keyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads and derives the public key for an existing RSA record.
    /// </summary>
    private async ValueTask<byte[]> LoadExistingPublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        var publicKey = await TryLoadPublicKeyAsync(
                GetPrivateKeyId(keyId),
                cancellationToken)
            .ConfigureAwait(false);

        return publicKey ?? throw new KeyNotFoundException(
            $"RSA key pair '{keyId}' was not found.");
    }

    /// <summary>
    /// Loads a derived public key or provisions a new RSA pair when missing.
    /// </summary>
    private async ValueTask<byte[]> LoadOrCreatePublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        var privateKeyId = GetPrivateKeyId(keyId);
        var existing = await TryLoadPublicKeyAsync(
                privateKeyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        return await CreateOrLoadWinningPublicKeyAsync(
                keyId,
                privateKeyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads an existing RSA pair from persisted private-key material.
    /// </summary>
    private async ValueTask<RsaKeyPair> LoadExistingKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        var keyPair = await TryLoadKeyPairAsync(
                GetPrivateKeyId(keyId),
                cancellationToken)
            .ConfigureAwait(false);

        return keyPair ?? throw new KeyNotFoundException(
            $"RSA key pair '{keyId}' was not found.");
    }

    /// <summary>
    /// Loads an RSA pair or provisions a new pair when no private-key record exists.
    /// </summary>
    private async ValueTask<RsaKeyPair> LoadOrCreateKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        var privateKeyId = GetPrivateKeyId(keyId);
        var existing = await TryLoadKeyPairAsync(
                privateKeyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        return await CreateOrLoadWinningKeyPairAsync(
                keyId,
                privateKeyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a stored private key and materializes a full RSA pair.
    /// </summary>
    private async ValueTask<RsaKeyPair?> TryLoadKeyPairAsync(
        string privateKeyId,
        CancellationToken cancellationToken)
    {
        var privateKey = await _storage
            .TryLoadAsync(privateKeyId, cancellationToken)
            .ConfigureAwait(false);

        if (privateKey is null)
        {
            return null;
        }

        try
        {
            return CreateKeyPairFromPrivateKey(privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    /// <summary>
    /// Loads a stored private key and derives only its public representation.
    /// </summary>
    private async ValueTask<byte[]?> TryLoadPublicKeyAsync(
        string privateKeyId,
        CancellationToken cancellationToken)
    {
        var privateKey = await _storage
            .TryLoadAsync(privateKeyId, cancellationToken)
            .ConfigureAwait(false);

        if (privateKey is null)
        {
            return null;
        }

        try
        {
            return DerivePublicKey(privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    /// <summary>
    /// Publishes one generated RSA candidate using bounded create-only retries and
    /// returns only the resulting public key.
    /// </summary>
    private async ValueTask<byte[]> CreateOrLoadWinningPublicKeyAsync(
        string keyId,
        string privateKeyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reuse one RSA pair across the entire race. Repeated RSA generation would be
        // unnecessarily expensive during external create/delete churn.
        using var candidate = _generator.Generate(_keySize);

        for (var attempt = 0;
             attempt < KeyCreationRetryPolicy.MaximumAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryPublishCandidateAsync(
                    candidate,
                    privateKeyId,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return candidate.ExportPublicKey();
            }

            var winner = await TryLoadPublicKeyAsync(
                    privateKeyId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (winner is not null)
            {
                return winner;
            }

            // The winning record may have been deleted between the failed create and load.
            // Retry publication of the same candidate within the bounded policy.
        }

        throw KeyCreationRetryPolicy.CreateExhaustedException(
            "RSA key pair",
            keyId);
    }

    /// <summary>
    /// Publishes one generated RSA candidate using bounded create-only retries and
    /// returns the caller-owned winning pair.
    /// </summary>
    private async ValueTask<RsaKeyPair> CreateOrLoadWinningKeyPairAsync(
        string keyId,
        string privateKeyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidate = _generator.Generate(_keySize);

        try
        {
            for (var attempt = 0;
                 attempt < KeyCreationRetryPolicy.MaximumAttempts;
                 attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await TryPublishCandidateAsync(
                        candidate,
                        privateKeyId,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    // Ownership transfers to the caller after successful publication.
                    return candidate;
                }

                var winner = await TryLoadKeyPairAsync(
                        privateKeyId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (winner is not null)
                {
                    candidate.Dispose();
                    return winner;
                }

                // The winning record may have been deleted between the failed create and load.
                // Retry publication of the same RSA pair rather than regenerating it.
            }

            throw KeyCreationRetryPolicy.CreateExhaustedException(
                "RSA key pair",
                keyId);
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Attempts to atomically persist only the private portion of an RSA candidate.
    /// </summary>
    private async ValueTask<bool> TryPublishCandidateAsync(
        RsaKeyPair candidate,
        string privateKeyId,
        CancellationToken cancellationToken)
    {
        var candidatePrivateKey = candidate.ExportPrivateKey();

        try
        {
            return await _storage
                .CreateAsync(
                    privateKeyId,
                    candidatePrivateKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidatePrivateKey);
        }
    }

    /// <summary>
    /// Reconstructs a validated RSA pair from persisted PKCS#8 private-key material.
    /// </summary>
    private static RsaKeyPair CreateKeyPairFromPrivateKey(
        ReadOnlySpan<byte> privateKey)
    {
        var publicKey = DerivePublicKey(privateKey);

        try
        {
            return new RsaKeyPair(
                privateKey,
                publicKey);
        }
        finally
        {
            Array.Clear(publicKey);
        }
    }

    /// <summary>
    /// Validates PKCS#8 private-key material and derives its SubjectPublicKeyInfo public key.
    /// </summary>
    private static byte[] DerivePublicKey(
        ReadOnlySpan<byte> privateKey)
    {
        using var rsa = RSA.Create();

        rsa.ImportPkcs8PrivateKey(
            privateKey,
            out var bytesRead);

        if (bytesRead != privateKey.Length)
        {
            throw new CryptographicException(
                "RSA private key contains trailing or invalid data.");
        }

        if (!RsaKeyOptionsValidator.MeetsMinimumKeySize(rsa.KeySize))
        {
            throw new CryptographicException(
                $"RSA key size cannot be less than " +
                $"{RsaKeyOptionsValidator.MinimumKeySize} bits.");
        }

        return rsa.ExportSubjectPublicKeyInfo();
    }

    /// <summary>
    /// Maps a logical RSA identifier to the private-key storage identifier.
    /// </summary>
    private static string GetPrivateKeyId(string keyId)
    {
        return keyId + PrivateKeySuffix;
    }
}
