using CryptoKit.Aes;
using CryptoKit.Hmac;
using CryptoKit.Rsa;
using CryptoKit.Storage;
using CryptoKit.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CryptoKit.Tests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCryptoKitAes_RegistersGeneratorAndProvider()
    {
        using var storage = new InMemoryKeyStorage();
        var services = new ServiceCollection();

        services.AddSingleton<IKeyStorage>(storage);
        services.AddCryptoKitAes();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<AesKeyGenerator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IAesKeyProvider>());
    }

    [Fact]
    public void AddCryptoKitHmac_RegistersGeneratorAndProvider()
    {
        using var storage = new InMemoryKeyStorage();
        var services = new ServiceCollection();

        services.AddSingleton<IKeyStorage>(storage);
        services.AddCryptoKitHmac();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<HmacKeyGenerator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IHmacKeyProvider>());
    }

    [Fact]
    public void AddCryptoKitRsa_RegistersGeneratorAndProvider()
    {
        using var storage = new InMemoryKeyStorage();
        var services = new ServiceCollection();

        services.AddSingleton<IKeyStorage>(storage);
        services.AddCryptoKitRsa();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<RsaKeyGenerator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IRsaKeyProvider>());
    }

    [Fact]
    public async Task AddCryptoKitAes_TakesSnapshotOfConfiguredOptions()
    {
        using var storage = new InMemoryKeyStorage();
        var services = new ServiceCollection();
        AesKeyOptions? configuredOptions = null;

        services.AddSingleton<IKeyStorage>(storage);
        services.AddCryptoKitAes(options =>
        {
            configuredOptions = options;
            options.KeySize = 128;
        });

        // После регистрации меняем объект, который был доступен configure.
        // Провайдер не должен увидеть это изменение: extension делает снимок.
        configuredOptions!.KeySize = 256;

        using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IAesKeyProvider>();

        using var key = await provider.GetOrCreateKeyAsync("snapshot");

        Assert.Equal(128, key.KeySize);
    }

    [Fact]
    public void AddCryptoKitFileStorage_RegistersFileKeyStorageAsIKeyStorage()
    {
        using var directory = new TemporaryDirectory();
        var services = new ServiceCollection();

        services.AddCryptoKitFileStorage(
            options => options.DirectoryPath = directory.Path);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<FileKeyStorage>(
            serviceProvider.GetRequiredService<IKeyStorage>());
    }

    [Fact]
    public void AddCryptoKitFileStorage_WithoutConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddCryptoKitFileStorage(null!));
    }
}
