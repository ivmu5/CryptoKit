using CryptoKit.Hmac;
using Xunit;

namespace CryptoKit.Tests.Hmac;

/// <summary>
/// Verifies HMAC key generation limits and requested key sizes.
/// </summary>
public sealed class HmacKeyGeneratorTests
{
    [Fact]
    public void Generate_WithoutSpecifiedSize_Returns256BitKey()
    {
        var generator = new HmacKeyGenerator();

        using var key = generator.Generate();

        Assert.Equal(256, key.KeySize);
        Assert.Equal(32, key.Length);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(65536)]
    public void Generate_WithValidSize_ReturnsRequestedSize(int keySize)
    {
        var generator = new HmacKeyGenerator();

        using var key = generator.Generate(keySize);

        Assert.Equal(keySize, key.KeySize);
        Assert.Equal(keySize / 8, key.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(120)]
    [InlineData(129)]
    [InlineData(65544)]
    public void Generate_WithInvalidSize_ThrowsArgumentOutOfRangeException(
        int keySize)
    {
        var generator = new HmacKeyGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => generator.Generate(keySize));
    }
}
