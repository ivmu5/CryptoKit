namespace CryptoKit.Aes;

/// <summary>
/// Configures generation of new AES keys.
/// </summary>
public sealed class AesKeyOptions
{
    /// <summary>
    /// Gets or sets the size, in bits, of newly generated AES keys.
    /// </summary>
    /// <remarks>
    /// Supported values are 128, 192, and 256. This option is used only when
    /// <see cref="IAesKeyProvider.GetOrCreateKeyAsync"/> needs to create a missing key;
    /// existing stored key material is validated independently of this setting.
    /// </remarks>
    public int KeySize { get; set; } = 256;
}
