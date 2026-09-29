using CryptoKit.Secrets;

namespace CryptoKit.Hmac;

/// <summary>
/// Represents caller-owned HMAC key material with an explicit lifetime.
/// </summary>
/// <remarks>
/// Dispose the instance when the key material is no longer required.
/// </remarks>
public sealed class HmacKey : SecretKeyMaterial
{
    /// <summary>
    /// Creates an HMAC key from existing key material.
    /// </summary>
    /// <param name="value">The HMAC key bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The supplied key material is shorter than 128 bits or its size is not byte-aligned.
    /// </exception>
    public HmacKey(ReadOnlySpan<byte> value)
        : base(CreateOwnedBuffer(value))
    {
        KeySize = checked(value.Length * 8);
    }

    /// <summary>
    /// Gets the HMAC key size in bits.
    /// </summary>
    public int KeySize { get; }

    /// <summary>
    /// Validates the supplied key material and creates the buffer owned by this instance.
    /// </summary>
    private static byte[] CreateOwnedBuffer(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException(
                "HMAC key material cannot be empty.",
                nameof(value));
        }

        var keySize = checked(value.Length * 8);

        HmacKeyOptionsValidator.ValidateKeyMaterialSize(
            keySize,
            nameof(value));

        return value.ToArray();
    }
}
