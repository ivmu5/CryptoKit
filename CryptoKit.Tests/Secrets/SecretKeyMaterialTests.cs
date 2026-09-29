using CryptoKit.Aes;
using Xunit;

namespace CryptoKit.Tests.Secrets;

/// <summary>
/// Verifies the shared SecretKeyMaterial contract through AesKey, a public derived type.
/// </summary>
public sealed class SecretKeyMaterialTests
{
    [Fact]
    public void CopyTo_CopiesMaterialIntoDestination()
    {
        var source = Enumerable.Range(1, 32)
            .Select(static value => (byte)value)
            .ToArray();

        using var key = new AesKey(source);
        var destination = new byte[32];

        key.CopyTo(destination);

        Assert.Equal(source, destination);
    }

    [Fact]
    public void CopyTo_WithTooSmallDestination_ThrowsArgumentException()
    {
        using var key = new AesKey(new byte[32]);

        Assert.Throws<ArgumentException>(
            () => key.CopyTo(new byte[31]));
    }

    [Fact]
    public void Dispose_MarksObjectAsDisposed()
    {
        var key = new AesKey(new byte[32]);

        key.Dispose();

        Assert.True(key.IsDisposed);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var key = new AesKey(new byte[32]);

        key.Dispose();
        key.Dispose();

        Assert.True(key.IsDisposed);
    }

    [Fact]
    public void Export_AfterDispose_ThrowsObjectDisposedException()
    {
        var key = new AesKey(new byte[32]);
        key.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => key.Export());
    }

    [Fact]
    public void CopyTo_AfterDispose_ThrowsObjectDisposedException()
    {
        var key = new AesKey(new byte[32]);
        key.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => key.CopyTo(new byte[32]));
    }
}
