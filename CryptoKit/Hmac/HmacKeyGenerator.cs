using System.Security.Cryptography;

namespace CryptoKit.Hmac;

/// <summary>
/// Выполняет создание новых HMAC-ключей.
/// </summary>
public sealed class HmacKeyGenerator
{
    /// <summary>
    /// Создаёт новый криптографически стойкий HMAC-ключ.
    /// </summary>
    /// <param name="keySize">
    /// Размер HMAC-ключа в битах.
    /// Допустимый диапазон для генерации: от 128 до 65536 бит включительно.
    /// Значение должно быть кратно восьми.
    /// </param>
    /// <returns>
    /// Новый HMAC-ключ.
    /// Вызывающий код обязан освободить его через <see cref="IDisposable.Dispose"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Возникает, если размер ключа меньше 128 бит, больше 65536 бит
    /// либо не кратен восьми.
    /// </exception>
    public HmacKey Generate(int keySize = 256)
    {
        HmacKeyOptionsValidator.ValidateGeneratedKeySize(
            keySize,
            nameof(keySize));

        var keyData = RandomNumberGenerator.GetBytes(
            keySize / 8);

        try
        {
            return new HmacKey(keyData);
        }
        finally
        {
            // HmacKey сохраняет собственную копию,
            // поэтому временный массив можно очистить.
            CryptographicOperations.ZeroMemory(keyData);
        }
    }
}

