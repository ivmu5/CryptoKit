using CryptoKit.Secrets;

namespace CryptoKit.Hmac;

/// <summary>
/// Представляет секретный HMAC-ключ с явным временем жизни.
/// </summary>
/// <remarks>
/// Экземпляр необходимо освобождать через <see cref="IDisposable.Dispose"/>,
/// когда ключевой материал больше не требуется.
/// </remarks>
public sealed class HmacKey : SecretKeyMaterial
{
    /// <summary>
    /// Создаёт объект HMAC-ключа.
    /// </summary>
    /// <param name="value">
    /// Бинарные данные HMAC-ключа.
    /// </param>
    public HmacKey(ReadOnlySpan<byte> value)
        : base(CreateOwnedBuffer(value))
    {
        KeySize = checked(value.Length * 8);
    }

    /// <summary>
    /// Размер HMAC-ключа в битах.
    /// </summary>
    public int KeySize { get; }

    private static byte[] CreateOwnedBuffer(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException(
                "HMAC-ключ не может быть пустым.",
                nameof(value));
        }

        var keySize = checked(value.Length * 8);

        HmacKeyOptionsValidator.ValidateKeyMaterialSize(
            keySize,
            nameof(value));

        return value.ToArray();
    }
}

