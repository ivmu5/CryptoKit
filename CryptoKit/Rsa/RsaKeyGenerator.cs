using System.Security.Cryptography;

namespace CryptoKit.Rsa;

/// <summary>
/// Generates RSA private/public key pairs.
/// </summary>
public sealed class RsaKeyGenerator
{
    /// <summary>
    /// Generates a new RSA key pair.
    /// </summary>
    /// <param name="keySize">The RSA key size in bits.</param>
    /// <returns>
    /// A new caller-owned RSA key pair. The caller must dispose it after use.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="keySize"/> is below the CryptoKit minimum or is not supported
    /// by the current RSA implementation.
    /// </exception>
    public RsaKeyPair Generate(int keySize = 3072)
    {
        RsaKeyOptionsValidator.ValidateKeySize(
            keySize,
            nameof(keySize));

        using var rsa = RSA.Create(keySize);

        // PKCS#8 is used as the persisted/private representation throughout CryptoKit.
        var privateKey = rsa.ExportPkcs8PrivateKey();

        try
        {
            // SubjectPublicKeyInfo contains only the public portion of the RSA key.
            var publicKey = rsa.ExportSubjectPublicKeyInfo();

            return new RsaKeyPair(
                privateKey,
                publicKey);
        }
        finally
        {
            // RsaKeyPair owns a copy, so the temporary private-key buffer can be cleared.
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }
}
