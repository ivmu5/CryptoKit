using System.Security.Cryptography;
using CryptoKit.Aes;
using Xunit;

namespace CryptoKit.Tests.Aes;

/// <summary>
/// Verifies AES key validation, ownership, and exported-copy semantics.
/// </summary>
public sealed class AesKeyTests
{
    [Theory]
    [InlineData(16, 128)]
    [InlineData(24, 192)]
    [InlineData(32, 256)]
    public void Constructor_WithValidMaterial_SetsExpectedSize(
        int length,
        int expectedKeySize)
    {
        var source = new byte[length];

        using var key = new AesKey(source);

        Assert.Equal(expectedKeySize, key.KeySize);
        Assert.Equal(length, key.Length);
    }

    [Fact]
    public void Constructor_WithEmptyMaterial_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new AesKey(ReadOnlySpan<byte>.Empty));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(31)]
    [InlineData(33)]
    public void Constructor_WithInvalidNonEmptyLength_ThrowsArgumentOutOfRangeException(
        int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AesKey(new byte[length]));
    }

    [Fact]
    public void Constructor_CopiesSourceArray()
    {
        var source = new byte[32];
        source[0] = 123;

        using var key = new AesKey(source);

        source[0] = 55;
        var exported = key.Export();

        try
        {
            Assert.Equal(123, exported[0]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(exported);
        }
    }

    [Fact]
    public void Export_ReturnsIndependentCopy()
    {
        var source = new byte[32];
        source[0] = 10;

        using var key = new AesKey(source);

        var first = key.Export();
        try
        {
            first[0] = 99;

            var second = key.Export();
            try
            {
                Assert.Equal(10, second[0]);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(second);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
        }
    }
}
