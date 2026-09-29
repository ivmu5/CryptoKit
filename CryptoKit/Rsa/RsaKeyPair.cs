using System.Security.Cryptography;
using CryptoKit.Secrets;

namespace CryptoKit.Rsa;

/// <summary>
/// Represents a caller-owned RSA private/public key pair.
/// </summary>
/// <remarks>
/// The private key is stored in owned secret memory and cleared on <see cref="Dispose"/>.
/// The public key is not secret, but it is still exposed only through caller-owned copies.
/// </remarks>
public sealed class RsaKeyPair : IDisposable
{
    private readonly PrivateRsaKeyMaterial _privateKey;
    private readonly byte[] _publicKey;

    /// <summary>
    /// Creates an RSA key pair from PKCS#8 private-key material and the matching
    /// SubjectPublicKeyInfo public key.
    /// </summary>
    /// <param name="privateKey">The RSA private key in PKCS#8 format.</param>
    /// <param name="publicKey">The RSA public key in SubjectPublicKeyInfo format.</param>
    /// <exception cref="ArgumentException">
    /// Either input is empty, or the supplied public key does not match the private key.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Either key has an invalid RSA encoding, or the private key is below the
    /// minimum size permitted by CryptoKit.
    /// </exception>
    public RsaKeyPair(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> publicKey)
    {
        if (privateKey.IsEmpty)
        {
            throw new ArgumentException(
                "RSA private key cannot be empty.",
                nameof(privateKey));
        }

        if (publicKey.IsEmpty)
        {
            throw new ArgumentException(
                "RSA public key cannot be empty.",
                nameof(publicKey));
        }

        ValidateKeyPair(privateKey, publicKey);

        var privateMaterial = new PrivateRsaKeyMaterial(privateKey);

        try
        {
            _publicKey = publicKey.ToArray();
            _privateKey = privateMaterial;
        }
        catch
        {
            privateMaterial.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the length, in bytes, of the PKCS#8 private-key representation.
    /// </summary>
    public int PrivateKeyLength => _privateKey.Length;

    /// <summary>
    /// Gets the length, in bytes, of the SubjectPublicKeyInfo representation.
    /// </summary>
    public int PublicKeyLength => _publicKey.Length;

    /// <summary>
    /// Gets whether the owned private-key material has been disposed.
    /// </summary>
    public bool IsDisposed => _privateKey.IsDisposed;

    /// <summary>
    /// Copies the private key into a caller-provided buffer.
    /// </summary>
    /// <param name="destination">
    /// A destination buffer at least <see cref="PrivateKeyLength"/> bytes long.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too small.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The private-key material has already been disposed.
    /// </exception>
    public void CopyPrivateKeyTo(Span<byte> destination)
    {
        _privateKey.CopyTo(destination);
    }

    /// <summary>
    /// Exports a caller-owned copy of the private key in PKCS#8 format.
    /// </summary>
    /// <returns>
    /// A new byte array that the caller is responsible for clearing after use.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// The private-key material has already been disposed.
    /// </exception>
    public byte[] ExportPrivateKey()
    {
        return _privateKey.Export();
    }

    /// <summary>
    /// Copies the public key into a caller-provided buffer.
    /// </summary>
    /// <param name="destination">
    /// A destination buffer at least <see cref="PublicKeyLength"/> bytes long.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too small.
    /// </exception>
    public void CopyPublicKeyTo(Span<byte> destination)
    {
        if (destination.Length < _publicKey.Length)
        {
            throw new ArgumentException(
                $"Destination buffer must contain at least {_publicKey.Length} bytes.",
                nameof(destination));
        }

        _publicKey.AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Exports a caller-owned copy of the public key in SubjectPublicKeyInfo format.
    /// </summary>
    public byte[] ExportPublicKey()
    {
        return _publicKey.ToArray();
    }

    /// <summary>
    /// Clears the owned private-key buffer. Repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        _privateKey.Dispose();
    }

    /// <summary>
    /// Validates key encodings, the minimum private-key size, and correspondence
    /// between the supplied private and public keys.
    /// </summary>
    private static void ValidateKeyPair(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> publicKey)
    {
        using var privateRsa = RSA.Create();
        using var publicRsa = RSA.Create();

        privateRsa.ImportPkcs8PrivateKey(
            privateKey,
            out var privateBytesRead);

        publicRsa.ImportSubjectPublicKeyInfo(
            publicKey,
            out var publicBytesRead);

        if (privateBytesRead != privateKey.Length)
        {
            throw new CryptographicException(
                "RSA private key contains trailing or invalid data.");
        }

        if (publicBytesRead != publicKey.Length)
        {
            throw new CryptographicException(
                "RSA public key contains trailing or invalid data.");
        }

        // Apply the minimum-size policy to imported material as well as generated keys;
        // otherwise weak persisted keys could bypass CryptoKit policy through storage
        // or direct construction.
        if (!RsaKeyOptionsValidator.MeetsMinimumKeySize(privateRsa.KeySize))
        {
            throw new CryptographicException(
                $"RSA key size cannot be less than " +
                $"{RsaKeyOptionsValidator.MinimumKeySize} bits.");
        }

        var expectedPublicKey = privateRsa.ExportSubjectPublicKeyInfo();
        var actualPublicKey = publicRsa.ExportSubjectPublicKeyInfo();

        try
        {
            if (!expectedPublicKey
                .AsSpan()
                .SequenceEqual(actualPublicKey))
            {
                throw new ArgumentException(
                    "RSA public key does not match the private key.",
                    nameof(publicKey));
            }
        }
        finally
        {
            Array.Clear(expectedPublicKey);
            Array.Clear(actualPublicKey);
        }
    }

    /// <summary>
    /// Adapts RSA private-key bytes to the shared secret-material lifetime implementation.
    /// </summary>
    private sealed class PrivateRsaKeyMaterial : SecretKeyMaterial
    {
        /// <summary>
        /// Creates owned secret material from a copy of the supplied private-key bytes.
        /// </summary>
        internal PrivateRsaKeyMaterial(ReadOnlySpan<byte> value)
            : base(value.ToArray())
        {
        }
    }
}
