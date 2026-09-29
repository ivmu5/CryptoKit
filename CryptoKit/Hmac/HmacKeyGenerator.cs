using System.Security.Cryptography;

namespace CryptoKit.Hmac;

/// <summary>
/// Generates cryptographically random HMAC keys.
/// </summary>
public sealed class HmacKeyGenerator
{
    /// <summary>
    /// Generates a new HMAC key of the requested size.
    /// </summary>
    /// <param name="keySize">
    /// The key size in bits. The generation range is 128 through 65,536 bits,
    /// and the value must be divisible by eight.
    /// </param>
    /// <returns>
    /// A new caller-owned HMAC key that must be disposed after use.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="keySize"/> is below 128 bits, exceeds the generation limit,
    /// or is not divisible by eight.
    /// </exception>
    public HmacKey Generate(int keySize = 256)
    {
        HmacKeyOptionsValidator.ValidateGeneratedKeySize(
            keySize,
            nameof(keySize));

        var keyData = RandomNumberGenerator.GetBytes(
            keySize / 8);

        try
        {
            return new HmacKey(keyData);
        }
        finally
        {
            // HmacKey owns a copy, so the temporary generation buffer can be cleared immediately.
            CryptographicOperations.ZeroMemory(keyData);
        }
    }
}
