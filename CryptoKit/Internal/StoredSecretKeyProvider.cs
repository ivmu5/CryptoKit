using System.Security.Cryptography;
using CryptoKit.Secrets;
using CryptoKit.Storage;

namespace CryptoKit.Internal;

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
            $"{_keyDescription} с идентификатором '{keyId}' не найден.");
    }

    internal async ValueTask<TKey> GetOrCreateAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);

        // Локальная сериализация уменьшает лишнюю работу внутри одного
        // экземпляра провайдера. Межпроцессная корректность создания
        // обеспечивается атомарным IKeyStorage.CreateAsync.
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

    private async ValueTask<TKey> CreateOrLoadWinnerAsync(
        string keyId,
        string storageKeyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Кандидат создаётся один раз и переиспользуется между повторными
        // атомарными попытками. Это особенно важно для потенциально дорогих
        // генераторов и не оставляет создание в бесконечном цикле.
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
                    return candidate;
                }

                // Другой экземпляр провайдера или процесс первым создал запись.
                // Если запись ещё существует, именно она является победителем.
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

                // Между проигранным CreateAsync и чтением запись могла быть
                // удалена. Повторяем публикацию того же кандидата ограниченное
                // число раз вместо генерации нового ключа на каждом круге.
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

    private CryptographicException CreateInvalidStoredMaterialException(
        string keyId,
        Exception innerException)
    {
        return new CryptographicException(
            $"Сохранённый {_keyDescription} с идентификатором '{keyId}' содержит недопустимый ключевой материал.",
            innerException);
    }

    private string GetStorageKeyId(string keyId)
    {
        return keyId + _storageSuffix;
    }
}
