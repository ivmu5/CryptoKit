using System.Security.Cryptography;
using CryptoKit.Rsa;
using CryptoKit.Internal;
using CryptoKit.Tests.Helpers;
using Xunit;

namespace CryptoKit.Tests.Rsa;

/// <summary>
/// Verifies RSA key-provider persistence, creation races, and public-key derivation.
/// </summary>
public sealed class RsaKeyProviderTests
{
    [Fact]
    public async Task GetKeyPairAsync_WhenMissing_ThrowsKeyNotFoundException()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 2048);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => provider.GetKeyPairAsync("missing").AsTask());
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenMissing_ThrowsKeyNotFoundException()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 2048);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => provider.GetPublicKeyAsync("missing").AsTask());
    }

    [Fact]
    public async Task GetOrCreateKeyPairAsync_AndGetPublicKeyAsync_ReturnMatchingPublicKey()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 2048);

        using var pair = await provider.GetOrCreateKeyPairAsync("auth");

        var expectedPublic = pair.ExportPublicKey();
        var actualPublic = await provider.GetPublicKeyAsync("auth");

        Assert.Equal(expectedPublic, actualPublic);
        Assert.Equal(1, storage.Count);
    }

    [Fact]
    public async Task ExistingPair_IsNotRegeneratedWhenGenerationSizeChanges()
    {
        using var storage = new InMemoryKeyStorage();

        var firstProvider = CreateProvider(storage, 2048);
        using (var created = await firstProvider.GetOrCreateKeyPairAsync("auth"))
        {
            Assert.Equal(2048, GetPrivateKeySize(created));
        }

        var secondProvider = CreateProvider(storage, 3072);
        using var loaded = await secondProvider.GetOrCreateKeyPairAsync("auth");

        Assert.Equal(2048, GetPrivateKeySize(loaded));
    }

    [Fact]
    public async Task GetKeyPairAsync_WithCorruptedStoredPrivateKey_ThrowsCryptographicException()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 2048);

        using (await provider.GetOrCreateKeyPairAsync("broken"))
        {
        }

        storage.ReplaceOnlyEntry(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<CryptographicException>(
            () => provider.GetKeyPairAsync("broken").AsTask());
    }

    [Fact]
    public async Task GetOrCreateKeyPairAsync_FromTwoProviders_ConvergesToOneStoredPair()
    {
        using var storage = new InMemoryKeyStorage();

        var firstProvider = CreateProvider(storage, 2048);
        var secondProvider = CreateProvider(storage, 2048);

        var firstTask = firstProvider.GetOrCreateKeyPairAsync("race").AsTask();
        var secondTask = secondProvider.GetOrCreateKeyPairAsync("race").AsTask();

        var pairs = await Task.WhenAll(firstTask, secondTask);

        try
        {
            Assert.Equal(1, storage.Count);
            Assert.Equal(
                pairs[0].ExportPublicKey(),
                pairs[1].ExportPublicKey());
        }
        finally
        {
            foreach (var pair in pairs)
            {
                pair.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetOrCreatePublicKeyAsync_CreatesPrivateRecordAndCanLaterReturnPair()
    {
        using var storage = new InMemoryKeyStorage();
        var provider = CreateProvider(storage, 2048);

        var publicKey = await provider.GetOrCreatePublicKeyAsync("auth");

        using var pair = await provider.GetKeyPairAsync("auth");

        Assert.Equal(publicKey, pair.ExportPublicKey());
        Assert.Equal(1, storage.Count);
    }

    [Fact]
    public async Task GetOrCreateKeyPairAsync_WithPermanentCreateConflict_StopsAfterBoundedRetries()
    {
        var storage = new AlwaysContendedKeyStorage();
        var provider = new RsaKeyProvider(
            storage,
            new RsaKeyGenerator(),
            new RsaKeyOptions { KeySize = 2048 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetOrCreateKeyPairAsync("unstable").AsTask());

        Assert.Equal(
            KeyCreationRetryPolicy.MaximumAttempts,
            storage.CreateCallCount);
    }

    [Fact]
    public async Task GetOrCreatePublicKeyAsync_WithPermanentCreateConflict_StopsAfterBoundedRetries()
    {
        var storage = new AlwaysContendedKeyStorage();
        var provider = new RsaKeyProvider(
            storage,
            new RsaKeyGenerator(),
            new RsaKeyOptions { KeySize = 2048 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetOrCreatePublicKeyAsync("unstable").AsTask());

        Assert.Equal(
            KeyCreationRetryPolicy.MaximumAttempts,
            storage.CreateCallCount);
    }

    private static RsaKeyProvider CreateProvider(
        InMemoryKeyStorage storage,
        int keySize)
    {
        return new RsaKeyProvider(
            storage,
            new RsaKeyGenerator(),
            new RsaKeyOptions
            {
                KeySize = keySize
            });
    }

    private static int GetPrivateKeySize(RsaKeyPair pair)
    {
        var privateKey = pair.ExportPrivateKey();

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKey, out _);
            return rsa.KeySize;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }
}
