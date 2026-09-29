namespace CryptoKit.Aes;

/// <summary>
/// Представляет настройки создания AES-ключей.
/// </summary>
public sealed class AesKeyOptions
{
    /// <summary>
    /// Размер нового AES-ключа в битах.
    /// Допустимые значения: 128, 192 или 256.
    /// </summary>
    /// <remarks>
    /// Значение используется только при создании отсутствующего ключа через
    /// <see cref="IAesKeyProvider.GetOrCreateKeyAsync"/>.
    /// Уже существующий ключ проверяется независимо от этой настройки.
    /// </remarks>
    public int KeySize { get; set; } = 256;
}
