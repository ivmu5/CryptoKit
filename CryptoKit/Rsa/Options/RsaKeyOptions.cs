namespace CryptoKit.Rsa;

/// <summary>
/// Представляет настройки создания RSA-ключей.
/// </summary>
public sealed class RsaKeyOptions
{
    /// <summary>
    /// Размер новой RSA-пары в битах.
    /// Значение должно быть не меньше 2048 бит и поддерживаться
    /// текущей реализацией RSA.
    /// </summary>
    /// <remarks>
    /// Значение используется только при создании отсутствующего ключа через
    /// <see cref="IRsaKeyProvider.GetOrCreateKeyPairAsync"/> или
    /// <see cref="IRsaKeyProvider.GetOrCreatePublicKeyAsync"/>.
    /// Уже существующий ключ проверяется независимо от этой настройки.
    /// </remarks>
    public int KeySize { get; set; } = 3072;
}
