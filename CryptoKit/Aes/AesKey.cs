using CryptoKit.Secrets;

namespace CryptoKit.Aes;

/// <summary>
/// Представляет секретный AES-ключ с явным временем жизни.
/// </summary>
/// <remarks>
/// Экземпляр необходимо освобождать через <see cref="IDisposable.Dispose"/>,
/// когда ключевой материал больше не требуется.
/// </remarks>
public sealed class AesKey : SecretKeyMaterial
{
    /// <summary>
    /// Создаёт объект AES-ключа.
    /// </summary>
    /// <param name="value">
    /// Бинарные данные AES-ключа.
    /// </param>
    public AesKey(ReadOnlySpan<byte> value)
        : base(CreateOwnedBuffer(value))
    {
        KeySize = checked(value.Length * 8);
    }

    /// <summary>
    /// Размер AES-ключа в битах.
    /// </summary>
    public int KeySize { get; }

    private static byte[] CreateOwnedBuffer(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException(
                "AES-ключ не может быть пустым.",
                nameof(value));
        }

        var keySize = checked(value.Length * 8);

        AesKeyOptionsValidator.ValidateKeySize(
            keySize,
            nameof(value));

        return value.ToArray();
    }
}

