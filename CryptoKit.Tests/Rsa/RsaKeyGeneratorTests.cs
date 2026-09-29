using System.Security.Cryptography;
using CryptoKit.Rsa;
using Xunit;

namespace CryptoKit.Tests.Rsa;

public sealed class RsaKeyGeneratorTests
{
    [Fact]
    public void Generate_With2048Bits_ReturnsMatchingRsaPair()
    {
        var generator = new RsaKeyGenerator();

        using var pair = generator.Generate(2048);
        var privateKey = pair.ExportPrivateKey();
        var publicKey = pair.ExportPublicKey();

        try
        {
            using var privateRsa = RSA.Create();
            using var publicRsa = RSA.Create();

            privateRsa.ImportPkcs8PrivateKey(privateKey, out var privateRead);
            publicRsa.ImportSubjectPublicKeyInfo(publicKey, out var publicRead);

            Assert.Equal(privateKey.Length, privateRead);
            Assert.Equal(publicKey.Length, publicRead);
            Assert.Equal(2048, privateRsa.KeySize);
            Assert.Equal(
                privateRsa.ExportSubjectPublicKeyInfo(),
                publicRsa.ExportSubjectPublicKeyInfo());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    [Fact]
    public void Generate_WithoutSpecifiedSize_Uses3072Bits()
    {
        var generator = new RsaKeyGenerator();

        using var pair = generator.Generate();
        var privateKey = pair.ExportPrivateKey();

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKey, out _);

            Assert.Equal(3072, rsa.KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    [Fact]
    public void Generate_WithKeyBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        var generator = new RsaKeyGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => generator.Generate(1024));
    }
}
