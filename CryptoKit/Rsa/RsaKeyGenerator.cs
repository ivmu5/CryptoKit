using System.Security.Cryptography;

namespace CryptoKit.Rsa;

/// <summary>
/// Выполняет создание новых пар RSA-ключей.
/// </summary>
public sealed class RsaKeyGenerator
{
    /// <summary>
    /// Создаёт новую пару RSA-ключей.
    /// </summary>
    /// <param name="keySize">
    /// Размер RSA-ключа в битах.
    /// </param>
    /// <returns>
    /// Новая пара закрытого и открытого RSA-ключей.
    /// Вызывающий код обязан освободить её через <see cref="IDisposable.Dispose"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Возникает, если размер RSA-ключа меньше минимально допустимого
    /// либо не поддерживается текущей реализацией RSA.
    /// </exception>
    public RsaKeyPair Generate(int keySize = 3072)
    {
        RsaKeyOptionsValidator.ValidateKeySize(
            keySize,
            nameof(keySize));

        using var rsa = RSA.Create(keySize);

        // Экспортируем закрытый ключ в стандартном формате PKCS#8.
        var privateKey = rsa.ExportPkcs8PrivateKey();

        try
        {
            // Экспортируем только открытую часть ключа
            // в стандартном публичном формате.
            var publicKey = rsa.ExportSubjectPublicKeyInfo();

            return new RsaKeyPair(
                privateKey,
                publicKey);
        }
        finally
        {
            // RsaKeyPair создаёт собственную копию закрытого ключа,
            // поэтому временный массив после создания пары можно очистить.
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }
}
