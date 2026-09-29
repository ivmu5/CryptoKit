using CryptoKit.Aes;
using Xunit;

namespace CryptoKit.Tests.Aes;

/// <summary>
/// Verifies AES key generation for supported and unsupported key sizes.
/// </summary>
public sealed class AesKeyGeneratorTests
{
    [Fact]
    public void Generate_WithoutSpecifiedSize_Returns256BitKey()
    {
        // Arrange
        var generator = new AesKeyGenerator();

        // Act
        using var key = generator.Generate();

        // Assert
        Assert.Equal(256, key.KeySize);
        Assert.Equal(32, key.Length);
    }

    [Theory]
    [InlineData(128, 16)]
    [InlineData(192, 24)]
    [InlineData(256, 32)]
    public void Generate_WithValidSize_ReturnsRequestedKey(
        int keySize,
        int expectedLength)
    {
        var generator = new AesKeyGenerator();

        using var key = generator.Generate(keySize);

        Assert.Equal(keySize, key.KeySize);
        Assert.Equal(expectedLength, key.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(129)]
    [InlineData(512)]
    public void Generate_WithInvalidSize_ThrowsArgumentOutOfRangeException(
        int keySize)
    {
        var generator = new AesKeyGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => generator.Generate(keySize));
    }
}
