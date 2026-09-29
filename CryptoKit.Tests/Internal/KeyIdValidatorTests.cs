using CryptoKit.Internal;
using Xunit;

namespace CryptoKit.Tests.Internal;

/// <summary>
/// Verifies logical key-identifier validation, including malformed UTF-16 input.
/// </summary>
public sealed class KeyIdValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void Validate_WithBlankIdentifier_ThrowsArgumentException(
        string keyId)
    {
        Assert.Throws<ArgumentException>(
            () => KeyIdValidator.Validate(keyId));
    }

    [Fact]
    public void Validate_WithNullIdentifier_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => KeyIdValidator.Validate(null!));
    }

    [Fact]
    public void Validate_WithLoneHighSurrogate_ThrowsArgumentException()
    {
        var invalid = new string(new[] { '\uD800' });

        Assert.Throws<ArgumentException>(
            () => KeyIdValidator.Validate(invalid));
    }

    [Fact]
    public void Validate_WithLoneLowSurrogate_ThrowsArgumentException()
    {
        var invalid = new string(new[] { '\uDC00' });

        Assert.Throws<ArgumentException>(
            () => KeyIdValidator.Validate(invalid));
    }

    [Theory]
    [InlineData("master")]
    [InlineData("ключ")]
    [InlineData("🔑")]
    [InlineData("service/auth/master")]
    public void Validate_WithValidIdentifier_DoesNotThrow(string keyId)
    {
        KeyIdValidator.Validate(keyId);
    }
}
