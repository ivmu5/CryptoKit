using CryptoKit.Storage;
using Xunit;

namespace CryptoKit.Tests.Storage;

public sealed class FileKeyNameEncoderTests
{
    [Fact]
    public void Encode_SameIdentifier_ReturnsSameName()
    {
        var first = FileKeyNameEncoder.Encode("master");
        var second = FileKeyNameEncoder.Encode("master");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Encode_DifferentIdentifiers_ReturnDifferentNames()
    {
        Assert.NotEqual(
            FileKeyNameEncoder.Encode("first"),
            FileKeyNameEncoder.Encode("second"));
    }

    [Fact]
    public void Encode_Returns64CharacterHexSha256()
    {
        var encoded = FileKeyNameEncoder.Encode("master");

        Assert.Equal(64, encoded.Length);
        Assert.All(encoded, character => Assert.True(Uri.IsHexDigit(character)));
    }

    [Fact]
    public void Encode_DoesNotExposeOriginalIdentifier()
    {
        const string keyId = "service/auth/master";

        var encoded = FileKeyNameEncoder.Encode(keyId);

        Assert.DoesNotContain("service", encoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/", encoded);
    }

    [Fact]
    public void Encode_WithMalformedUtf16_ThrowsArgumentException()
    {
        var invalid = new string(new[] { '\uD800' });

        Assert.Throws<ArgumentException>(
            () => FileKeyNameEncoder.Encode(invalid));
    }
}
