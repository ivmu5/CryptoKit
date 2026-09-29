namespace CryptoKit.Rsa;

/// <summary>
/// Configures generation of new RSA key pairs.
/// </summary>
public sealed class RsaKeyOptions
{
    /// <summary>
    /// Gets or sets the size, in bits, of newly generated RSA key pairs.
    /// </summary>
    /// <remarks>
    /// The value must be at least 2048 bits and supported by the current RSA
    /// implementation. It is used only when a missing key is created through
    /// <see cref="IRsaKeyProvider.GetOrCreateKeyPairAsync"/> or
    /// <see cref="IRsaKeyProvider.GetOrCreatePublicKeyAsync"/>. Existing stored
    /// key material is validated independently of this setting.
    /// </remarks>
    public int KeySize { get; set; } = 3072;
}
