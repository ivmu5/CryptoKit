using System.Security.Cryptography;

namespace CryptoKit.Rsa;

/// <summary>
/// Определяет асинхронный источник RSA-ключей.
/// </summary>
public interface IRsaKeyProvider
{
    /// <summary>
    /// Асинхронно получает существующую пару RSA-ключей
    /// по указанному идентификатору.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный идентификатор пары RSA-ключей.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища.
    /// </param>
    /// <returns>
    /// Новый экземпляр существующей RSA-пары, принадлежащий вызывающему коду.
    /// Вызывающий код обязан вызвать <see cref="IDisposable.Dispose"/>
    /// после завершения работы с материалом закрытого ключа.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// RSA-ключ с указанным идентификатором отсутствует.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Сохранённый закрытый RSA-ключ имеет некорректный формат либо его размер
    /// меньше минимально допустимого для CryptoKit.
    /// </exception>
    ValueTask<RsaKeyPair> GetKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно получает существующую RSA-пару либо атомарно создаёт новую,
    /// если ключ с указанным идентификатором отсутствует.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный идентификатор пары RSA-ключей.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища или создания ключа.
    /// </param>
    /// <returns>
    /// Новый экземпляр RSA-пары, принадлежащий вызывающему коду.
    /// Вызывающий код обязан вызвать <see cref="IDisposable.Dispose"/>
    /// после завершения работы с материалом закрытого ключа.
    /// </returns>
    /// <remarks>
    /// Этот метод явно разрешает создание нового материала закрытого ключа.
    /// Для получения только ранее существовавшей пары используйте
    /// <see cref="GetKeyPairAsync"/>.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Существующий закрытый RSA-ключ имеет некорректный формат либо его размер
    /// меньше минимально допустимого для CryptoKit.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Не удалось завершить создание из-за непрерывных конкурентных изменений записи.
    /// </exception>
    ValueTask<RsaKeyPair> GetOrCreateKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно получает открытый ключ существующей RSA-пары,
    /// не предоставляя вызывающему коду доступ к закрытому ключу.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный идентификатор пары RSA-ключей.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища.
    /// </param>
    /// <returns>
    /// Новый массив байт, принадлежащий вызывающему коду, с открытым RSA-ключом
    /// в формате SubjectPublicKeyInfo.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// RSA-ключ с указанным идентификатором отсутствует.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Сохранённый закрытый RSA-ключ имеет некорректный формат либо его размер
    /// меньше минимально допустимого для CryptoKit.
    /// </exception>
    ValueTask<byte[]> GetPublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно получает открытый RSA-ключ либо создаёт новую RSA-пару,
    /// если ключ с указанным идентификатором отсутствует.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный идентификатор пары RSA-ключей.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища или создания ключа.
    /// </param>
    /// <returns>
    /// Новый массив байт, принадлежащий вызывающему коду, с открытым RSA-ключом
    /// в формате SubjectPublicKeyInfo.
    /// </returns>
    /// <remarks>
    /// Закрытый ключ новой пары сохраняется в настроенном хранилище,
    /// но не возвращается вызывающему коду.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Существующий закрытый RSA-ключ имеет некорректный формат либо его размер
    /// меньше минимально допустимого для CryptoKit.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Не удалось завершить создание из-за непрерывных конкурентных изменений записи.
    /// </exception>
    ValueTask<byte[]> GetOrCreatePublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
