using System.Security.Cryptography;

namespace CryptoKit.Aes;

/// <summary>
/// Определяет асинхронный источник AES-ключей.
/// </summary>
public interface IAesKeyProvider
{
    /// <summary>
    /// Асинхронно получает существующий AES-ключ
    /// по указанному идентификатору.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный логический идентификатор AES-ключа.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища.
    /// </param>
    /// <returns>
    /// Новый экземпляр существующего AES-ключа, принадлежащий вызывающему коду.
    /// Вызывающий код обязан вызвать <see cref="IDisposable.Dispose"/>
    /// после завершения работы с ключевым материалом.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Ключ с указанным идентификатором отсутствует.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Сохранённый AES-ключ содержит недопустимый ключевой материал.
    /// </exception>
    ValueTask<AesKey> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно получает существующий AES-ключ либо атомарно создаёт новый,
    /// если запись с указанным идентификатором отсутствует.
    /// </summary>
    /// <param name="keyId">
    /// Уникальный логический идентификатор AES-ключа.
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены ожидания хранилища или создания ключа.
    /// </param>
    /// <returns>
    /// Новый экземпляр AES-ключа, принадлежащий вызывающему коду.
    /// Вызывающий код обязан вызвать <see cref="IDisposable.Dispose"/>
    /// после завершения работы с ключевым материалом.
    /// </returns>
    /// <remarks>
    /// Этот метод явно разрешает создание нового ключевого материала.
    /// Для получения только ранее существовавшего ключа используйте
    /// <see cref="GetKeyAsync"/>.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Существующая запись AES-ключа содержит недопустимый ключевой материал.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Не удалось завершить создание из-за непрерывных конкурентных изменений записи.
    /// </exception>
    ValueTask<AesKey> GetOrCreateKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
