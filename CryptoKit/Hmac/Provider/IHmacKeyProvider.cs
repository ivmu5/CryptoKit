using System.Security.Cryptography;

namespace CryptoKit.Hmac;

/// <summary>
/// Определяет асинхронный источник HMAC-ключей.
/// </summary>
public interface IHmacKeyProvider
{
    /// <summary>
    /// Асинхронно получает существующий HMAC-ключ
    /// по указанному идентификатору.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный логический идентификатор HMAC-ключа.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища.
    /// </param>
    /// <returns>
    /// Новый экземпляр существующего HMAC-ключа, принадлежащий вызывающему коду.
    /// Вызывающий код обязан вызвать <see cref="IDisposable.Dispose"/>
    /// после завершения работы с ключевым материалом.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Ключ с указанным идентификатором отсутствует.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Сохранённый HMAC-ключ содержит недопустимый ключевой материал.
    /// </exception>
    ValueTask<HmacKey> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно получает существующий HMAC-ключ либо атомарно создаёт новый,
    /// если запись с указанным идентификатором отсутствует.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный логический идентификатор HMAC-ключа.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища или создания ключа.
    /// </param>
    /// <returns>
    /// Новый экземпляр HMAC-ключа, принадлежащий вызывающему коду.
    /// Вызывающий код обязан вызвать <see cref="IDisposable.Dispose"/>
    /// после завершения работы с ключевым материалом.
    /// </returns>
    /// <remarks>
    /// Этот метод явно разрешает создание нового ключевого материала.
    /// Для получения только ранее существовавшего ключа используйте
    /// <see cref="GetKeyAsync"/>.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Существующая запись HMAC-ключа содержит недопустимый ключевой материал.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Не удалось завершить создание из-за непрерывных конкурентных изменений записи.
    /// </exception>
    ValueTask<HmacKey> GetOrCreateKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
