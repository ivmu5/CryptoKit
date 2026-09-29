using System.Security.Cryptography;

namespace CryptoKit.Aes;

/// <summary>
/// Generates cryptographically random AES keys.
/// </summary>
public sealed class AesKeyGenerator
{
    /// <summary>
    /// Generates a new AES key of the requested size.
    /// </summary>
    /// <param name="keySize">
    /// The key size in bits. Supported values are 128, 192, and 256.
    /// </param>
    /// <returns>
    /// A new caller-owned AES key that must be disposed after use.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="keySize"/> is not a supported AES key size.
    /// </exception>
    public AesKey Generate(int keySize = 256)
    {
        AesKeyOptionsValidator.ValidateKeySize(
            keySize,
            nameof(keySize));

        // AES sizes are expressed in bits, while RandomNumberGenerator expects a byte count.
        var keyData = RandomNumberGenerator.GetBytes(
            keySize / 8);

        try
        {
            return new AesKey(keyData);
        }
        finally
        {
            // AesKey owns a copy, so the temporary generation buffer can be cleared immediately.
            CryptographicOperations.ZeroMemory(keyData);
        }
    }
}
