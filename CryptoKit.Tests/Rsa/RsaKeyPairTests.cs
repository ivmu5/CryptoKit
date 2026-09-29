using System.Security.Cryptography;
using CryptoKit.Rsa;
using Xunit;

namespace CryptoKit.Tests.Rsa;

/// <summary>
/// Verifies RSA key-pair validation, ownership, export, and disposal behavior.
/// </summary>
public sealed class RsaKeyPairTests
{
    [Fact]
    public void Constructor_WithMatchingPair_AcceptsMaterial()
    {
        var (privateKey, publicKey) = CreateMaterial();

        try
        {
            using var pair = new RsaKeyPair(privateKey, publicKey);

            Assert.True(pair.PrivateKeyLength > 0);
            Assert.True(pair.PublicKeyLength > 0);
            Assert.False(pair.IsDisposed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    [Fact]
    public void Constructor_WithMismatchedPublicKey_ThrowsArgumentException()
    {
        var (privateKey, _) = CreateMaterial();
        var otherPublicKey = CreatePublicMaterial();

        try
        {
            Assert.Throws<ArgumentException>(
                () => new RsaKeyPair(privateKey, otherPublicKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    [Fact]
    public void Constructor_WithMalformedPrivateKey_ThrowsCryptographicException()
    {
        var publicKey = CreatePublicMaterial();

        Assert.Throws<CryptographicException>(
            () => new RsaKeyPair(new byte[] { 1, 2, 3 }, publicKey));
    }

    [Fact]
    public void Constructor_WithPrivateKeyBelow2048Bits_ThrowsCryptographicException()
    {
        using var rsa = RSA.Create(1024);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        var publicKey = rsa.ExportSubjectPublicKeyInfo();

        try
        {
            Assert.Throws<CryptographicException>(
                () => new RsaKeyPair(privateKey, publicKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    [Fact]
    public void Dispose_BlocksPrivateExport_ButPublicPartRemainsAvailable()
    {
        var (privateKey, publicKey) = CreateMaterial();

        try
        {
            var pair = new RsaKeyPair(privateKey, publicKey);
            pair.Dispose();

            Assert.True(pair.IsDisposed);
            Assert.Throws<ObjectDisposedException>(
                () => pair.ExportPrivateKey());

            Assert.Equal(publicKey, pair.ExportPublicKey());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    private static (byte[] PrivateKey, byte[] PublicKey) CreateMaterial()
    {
        using var rsa = RSA.Create(2048);

        return (
            rsa.ExportPkcs8PrivateKey(),
            rsa.ExportSubjectPublicKeyInfo());
    }

    private static byte[] CreatePublicMaterial()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportSubjectPublicKeyInfo();
    }
}
