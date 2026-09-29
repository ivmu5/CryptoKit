using System.Security.Cryptography;
using CryptoKit.Internal;
using CryptoKit.Storage;

namespace CryptoKit.Rsa;

/// <summary>
/// Предоставляет асинхронный доступ к RSA-ключам через настроенное хранилище.
/// </summary>
/// <remarks>
/// В хранилище сохраняется только закрытый RSA-ключ в формате PKCS#8.
/// Открытый ключ является производным материалом и каждый раз вычисляется
/// из сохранённого закрытого ключа.
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
    /// Создаёт провайдер RSA-ключей.
    /// </summary>
    /// <param name="storage">Асинхронное хранилище ключевого материала.</param>
    /// <param name="generator">Генератор новых RSA-пар.</param>
    /// <param name="options">Настройки создаваемых RSA-ключей.</param>
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

    private async ValueTask<byte[]> LoadExistingPublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        var publicKey = await TryLoadPublicKeyAsync(
                GetPrivateKeyId(keyId),
                cancellationToken)
            .ConfigureAwait(false);

        return publicKey ?? throw new KeyNotFoundException(
            $"RSA-пара '{keyId}' не найдена.");
    }

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

    private async ValueTask<RsaKeyPair> LoadExistingKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken)
    {
        var keyPair = await TryLoadKeyPairAsync(
                GetPrivateKeyId(keyId),
                cancellationToken)
            .ConfigureAwait(false);

        return keyPair ?? throw new KeyNotFoundException(
            $"RSA-пара '{keyId}' не найдена.");
    }

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

    private async ValueTask<byte[]> CreateOrLoadWinningPublicKeyAsync(
        string keyId,
        string privateKeyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Для всей серии конфликтов используем одну RSA-пару. При внешнем
        // create/delete churn это исключает повторную дорогую генерацию ключа.
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

            // Между проигранным CreateAsync и чтением запись могла быть
            // удалена. Повторяем публикацию того же кандидата ограниченно.
        }

        throw KeyCreationRetryPolicy.CreateExhaustedException(
            "RSA-пара",
            keyId);
    }

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
                    // После успешного CreateAsync владение кандидатом передаётся
                    // вызывающему коду; здесь его освобождать нельзя.
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

                // Между проигранным CreateAsync и чтением запись могла быть
                // удалена. Повторяем публикацию той же RSA-пары ограниченно.
            }

            throw KeyCreationRetryPolicy.CreateExhaustedException(
                "RSA-пара",
                keyId);
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

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
                "Закрытый RSA-ключ содержит лишние или некорректные данные.");
        }

        if (!RsaKeyOptionsValidator.MeetsMinimumKeySize(rsa.KeySize))
        {
            throw new CryptographicException(
                $"Размер RSA-ключа не может быть меньше " +
                $"{RsaKeyOptionsValidator.MinimumKeySize} бит.");
        }

        return rsa.ExportSubjectPublicKeyInfo();
    }

    private static string GetPrivateKeyId(string keyId)
    {
        return keyId + PrivateKeySuffix;
    }
}
