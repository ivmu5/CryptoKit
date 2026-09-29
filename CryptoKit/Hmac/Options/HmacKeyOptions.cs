namespace CryptoKit.Hmac;

/// <summary>
/// Configures generation of new HMAC keys.
/// </summary>
public sealed class HmacKeyOptions
{
    /// <summary>
    /// Gets or sets the size, in bits, of newly generated HMAC keys.
    /// </summary>
    /// <remarks>
    /// The generation range is 128 through 65,536 bits and must be divisible by eight.
    /// The upper bound is an operational guard against accidental excessive allocation.
    /// It applies only to generation: existing stored key material is validated without
    /// applying this generation-only maximum. The default is 256 bits.
    /// </remarks>
    public int KeySize { get; set; } = 256;
}
