using System.Security.Cryptography;
using CryptoKit.Hmac;
using CryptoKit.Internal;
using CryptoKit.Tests.Helpers;
using Xunit;

namespace CryptoKit.Tests.Hmac;

/// <summary>
/// Verifies HMAC provider persistence, validation, and creation races.
/// </summary>
public sealed class HmacKeyProviderTests
{
    [Fact]
    public async Task GetKeyAsync_WhenKeyMissing_ThrowsKeyNotFoundException()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 256);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => provider.GetKeyAsync("missing").AsTask());
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_CalledAgain_ReturnsSameStoredMaterial()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 256);

        using var first = await provider.GetOrCreateKeyAsync("master");
        var expected = first.Export();

        try
        {
            using var second = await provider.GetKeyAsync("master");
            var actual = second.Export();

            try
            {
                Assert.Equal(expected, actual);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WithCorruptedStoredMaterial_ThrowsCryptographicException()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 256);

        using (await provider.GetOrCreateKeyAsync("broken"))
        {
        }

        storage.ReplaceOnlyEntry(new byte[1]);

        await Assert.ThrowsAsync<CryptographicException>(
            () => provider.GetKeyAsync("broken").AsTask());
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_FromSeveralProviders_ConvergesToOneKey()
    {
        using var storage = new InMemoryKeyStorage();

        var providers = Enumerable.Range(0, 8)
            .Select(_ => CreateProvider(storage, 256))
            .ToArray();

        var keys = await Task.WhenAll(
            providers.Select(
                provider => provider.GetOrCreateKeyAsync("race").AsTask()));

        try
        {
            Assert.Equal(1, storage.Count);

            var expected = keys[0].Export();
            try
            {
                foreach (var key in keys)
                {
                    var actual = key.Export();
                    try
                    {
                        Assert.Equal(expected, actual);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(actual);
                    }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
            }
        }
        finally
        {
            foreach (var key in keys)
            {
                key.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WithPermanentCreateConflict_StopsAfterBoundedRetries()
    {
        var storage = new AlwaysContendedKeyStorage();
        var provider = new HmacKeyProvider(
            storage,
            new HmacKeyGenerator(),
            new HmacKeyOptions { KeySize = 256 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetOrCreateKeyAsync("unstable").AsTask());

        Assert.Equal(
            KeyCreationRetryPolicy.MaximumAttempts,
            storage.CreateCallCount);
    }

    private static HmacKeyProvider CreateProvider(
        InMemoryKeyStorage storage,
        int keySize)
    {
        return new HmacKeyProvider(
            storage,
            new HmacKeyGenerator(),
            new HmacKeyOptions
            {
                KeySize = keySize
            });
    }
}
