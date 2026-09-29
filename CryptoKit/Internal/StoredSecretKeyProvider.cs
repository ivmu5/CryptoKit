using System.Security.Cryptography;
using CryptoKit.Secrets;
using CryptoKit.Storage;

namespace CryptoKit.Internal;

/// <summary>
/// Implements the shared persistence and create-or-load workflow used by
/// symmetric secret-key providers.
/// </summary>
/// <typeparam name="TKey">
/// The caller-owned secret key type materialized from storage.
/// </typeparam>
internal sealed class StoredSecretKeyProvider<TKey>
    where TKey : SecretKeyMaterial
{
    private readonly IKeyStorage _storage;
    private readonly Func<byte[], TKey> _materialFactory;
    private readonly Func<TKey> _generator;
    private readonly string _storageSuffix;
    private readonly string _keyDescription;

    private readonly KeyedLock _keyLocks =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a provider core for one secret-key type.
    /// </summary>
    /// <param name="storage">The persistence backend.</param>
    /// <param name="materialFactory">Creates a key object from stored bytes.</param>
    /// <param name="generator">Creates new key material when provisioning is allowed.</param>
    /// <param name="storageSuffix">The suffix used to isolate this key type in storage.</param>
    /// <param name="keyDescription">A human-readable key type used in diagnostics.</param>
    internal StoredSecretKeyProvider(
        IKeyStorage storage,
        Func<byte[], TKey> materialFactory,
        Func<TKey> generator,
        string storageSuffix,
        string keyDescription)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(materialFactory);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageSuffix);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyDescription);

        _storage = storage;
        _materialFactory = materialFactory;
        _generator = generator;
        _storageSuffix = storageSuffix;
        _keyDescription = keyDescription;
    }

    /// <summary>
    /// Loads an existing key and throws when no record exists.
    /// </summary>
    internal async ValueTask<TKey> GetAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        using var keyLock = await _keyLocks
            .AcquireAsync(keyId, cancellationToken)
            .ConfigureAwait(false);

        var existing = await TryLoadAsync(
                keyId,
                GetStorageKeyId(keyId),
                cancellationToken)
            .ConfigureAwait(false);

        return existing ?? throw new KeyNotFoundException(
            $"{_keyDescription} with identifier '{keyId}' was not found.");
    }

    /// <summary>
    /// Loads an existing key or atomically publishes a newly generated key.
    /// </summary>
    internal async ValueTask<TKey> GetOrCreateAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        // Local serialization avoids duplicate work inside one provider instance.
        // Correctness across provider instances or processes depends on the atomic
        // create-only contract of IKeyStorage.CreateAsync.
        using var keyLock = await _keyLocks
            .AcquireAsync(keyId, cancellationToken)
            .ConfigureAwait(false);

        var storageKeyId = GetStorageKeyId(keyId);
        var existing = await TryLoadAsync(
                keyId,
                storageKeyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        return await CreateOrLoadWinnerAsync(
                keyId,
                storageKeyId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loads and validates stored key material, returning <see langword="null"/>
    /// when the record does not exist.
    /// </summary>
    private async ValueTask<TKey?> TryLoadAsync(
        string keyId,
        string storageKeyId,
        CancellationToken cancellationToken)
    {
        var keyData = await _storage
            .TryLoadAsync(storageKeyId, cancellationToken)
            .ConfigureAwait(false);

        if (keyData is null)
        {
            return null;
        }

        try
        {
            try
            {
                return _materialFactory(keyData);
            }
            catch (ArgumentException exception)
            {
                throw CreateInvalidStoredMaterialException(
                    keyId,
                    exception);
            }
            catch (OverflowException exception)
            {
                throw CreateInvalidStoredMaterialException(
                    keyId,
                    exception);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyData);
        }
    }

    /// <summary>
    /// Publishes one generated candidate using bounded create-only retries and,
    /// after a lost race, loads the record published by the winning participant.
    /// </summary>
    private async ValueTask<TKey> CreateOrLoadWinnerAsync(
        string keyId,
        string storageKeyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reuse one candidate across retries. This avoids repeatedly generating expensive
        // key material and guarantees that pathological create/delete churn is bounded.
        var candidate = _generator();

        try
        {
            for (var attempt = 0;
                 attempt < KeyCreationRetryPolicy.MaximumAttempts;
                 attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var keyData = candidate.Export();
                bool created;

                try
                {
                    created = await _storage
                        .CreateAsync(
                            storageKeyId,
                            keyData,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(keyData);
                }

                if (created)
                {
                    // Ownership of candidate transfers to the caller on success.
                    return candidate;
                }

                // Another provider instance or process won the create-only race.
                // If that record still exists, it is the authoritative value.
                var winner = await TryLoadAsync(
                        keyId,
                        storageKeyId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (winner is not null)
                {
                    candidate.Dispose();
                    return winner;
                }

                // The winner may have been deleted between the failed create and the load.
                // Retry publication of the same candidate instead of regenerating material.
            }

            throw KeyCreationRetryPolicy.CreateExhaustedException(
                _keyDescription,
                keyId);
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Wraps validation failures from stored key material in a cryptographic exception
    /// that includes the logical key identifier.
    /// </summary>
    private CryptographicException CreateInvalidStoredMaterialException(
        string keyId,
        Exception innerException)
    {
        return new CryptographicException(
            $"Stored {_keyDescription} '{keyId}' contains invalid key material.",
            innerException);
    }

    /// <summary>
    /// Maps a logical key identifier to the algorithm-specific storage identifier.
    /// </summary>
    private string GetStorageKeyId(string keyId)
    {
        return keyId + _storageSuffix;
    }
}
