using System.Security.Cryptography;
using CryptoKit.Hmac;
using Xunit;

namespace CryptoKit.Tests.Hmac;

/// <summary>
/// Verifies HMAC key-material validation and ownership semantics.
/// </summary>
public sealed class HmacKeyTests
{
    [Fact]
    public void Constructor_With128BitMaterial_IsValid()
    {
        using var key = new HmacKey(new byte[16]);

        Assert.Equal(128, key.KeySize);
        Assert.Equal(16, key.Length);
    }

    [Fact]
    public void Constructor_WithMaterialAboveGenerationLimit_IsStillValid()
    {
        // The 65,536-bit limit applies to generation only; imported material may be larger.
        using var key = new HmacKey(new byte[8193]);

        Assert.Equal(8193 * 8, key.KeySize);
    }

    [Fact]
    public void Constructor_WithEmptyMaterial_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new HmacKey(ReadOnlySpan<byte>.Empty));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(15)]
    public void Constructor_WithMaterialBelow128Bits_ThrowsArgumentOutOfRangeException(
        int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HmacKey(new byte[length]));
    }

    [Fact]
    public void Constructor_CopiesSourceArray()
    {
        var source = new byte[32];
        source[0] = 42;

        using var key = new HmacKey(source);
        source[0] = 7;

        var exported = key.Export();
        try
        {
            Assert.Equal(42, exported[0]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(exported);
        }
    }
}
