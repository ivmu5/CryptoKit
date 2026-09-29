using CryptoKit.Secrets;

namespace CryptoKit.Aes;

/// <summary>
/// Represents caller-owned AES key material with an explicit lifetime.
/// </summary>
/// <remarks>
/// Dispose the instance when the key material is no longer required.
/// The key data is stored in an owned buffer managed by <see cref="SecretKeyMaterial"/>.
/// </remarks>
public sealed class AesKey : SecretKeyMaterial
{
    /// <summary>
    /// Creates an AES key from existing key material.
    /// </summary>
    /// <param name="value">The AES key bytes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The key size is not 128, 192, or 256 bits.
    /// </exception>
    public AesKey(ReadOnlySpan<byte> value)
        : base(CreateOwnedBuffer(value))
    {
        KeySize = checked(value.Length * 8);
    }

    /// <summary>
    /// Gets the AES key size in bits.
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
                "AES key material cannot be empty.",
                nameof(value));
        }

        var keySize = checked(value.Length * 8);

        AesKeyOptionsValidator.ValidateKeySize(
            keySize,
            nameof(value));

        return value.ToArray();
    }
}
