using System.Security.Cryptography;

namespace CryptoKit.Aes;

/// <summary>
/// Выполняет создание новых AES-ключей.
/// </summary>
public sealed class AesKeyGenerator
{
    /// <summary>
    /// Создаёт новый криптографически стойкий AES-ключ.
    /// </summary>
    /// <param name="keySize">
    /// Размер AES-ключа в битах.
    /// Допустимые значения: 128, 192 или 256.
    /// </param>
    /// <returns>
    /// Новый AES-ключ.
    /// Вызывающий код обязан освободить его через <see cref="IDisposable.Dispose"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Возникает, если указан недопустимый размер AES-ключа.
    /// </exception>
    public AesKey Generate(int keySize = 256)
    {
        AesKeyOptionsValidator.ValidateKeySize(
            keySize,
            nameof(keySize));

        // Для AES размер ключа задаётся в битах,
        // а генератор случайных данных принимает количество байт.
        var keyData = RandomNumberGenerator.GetBytes(
            keySize / 8);

        try
        {
            return new AesKey(keyData);
        }
        finally
        {
            // AesKey сохраняет собственную копию,
            // поэтому временный массив можно очистить.
            CryptographicOperations.ZeroMemory(keyData);
        }
    }
}

