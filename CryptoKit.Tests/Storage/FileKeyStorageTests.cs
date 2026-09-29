using CryptoKit.Storage;
using CryptoKit.Tests.Helpers;
using Xunit;

namespace CryptoKit.Tests.Storage;

/// <summary>
/// Verifies file-storage CRUD, atomicity, cancellation boundaries, permissions, and races.
/// </summary>
public sealed class FileKeyStorageTests
{
    [Fact]
    public void Constructor_WithoutDirectoryPath_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => new FileKeyStorage(new FileKeyStorageOptions()));
    }

    [Fact]
    public void Constructor_WhenDirectoryPathPointsToFile_ThrowsInvalidOperationException()
    {
        using var directory = new TemporaryDirectory();
        var file = System.IO.Path.Combine(directory.Path, "not-a-directory");
        File.WriteAllText(file, "test");

        Assert.Throws<InvalidOperationException>(
            () => new FileKeyStorage(
                new FileKeyStorageOptions
                {
                    DirectoryPath = file
                }));
    }

    [Fact]
    public void Constructor_WithNonPositiveMaximumSize_ThrowsInvalidOperationException()
    {
        using var directory = new TemporaryDirectory();

        Assert.Throws<InvalidOperationException>(
            () => new FileKeyStorage(
                new FileKeyStorageOptions
                {
                    DirectoryPath = directory.Path,
                    MaximumEntrySizeBytes = 0
                }));
    }

    [Fact]
    public async Task TryLoadAsync_WhenEntryMissing_ReturnsNull()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        var result = await storage.TryLoadAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ThenLoad_ReturnsStoredBytes()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);
        var data = new byte[] { 1, 2, 3, 4 };

        var created = await storage.CreateAsync("master", data);
        var loaded = await storage.TryLoadAsync("master");

        Assert.True(created);
        Assert.Equal(data, loaded);
    }

    [Fact]
    public async Task CreateAsync_WhenEntryAlreadyExists_ReturnsFalseAndPreservesOriginal()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        Assert.True(await storage.CreateAsync("master", new byte[] { 1, 2, 3 }));
        Assert.False(await storage.CreateAsync("master", new byte[] { 9, 9, 9 }));

        Assert.Equal(
            new byte[] { 1, 2, 3 },
            await storage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task ReplaceAsync_WhenEntryExists_ReplacesData()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        await storage.CreateAsync("master", new byte[] { 1, 2, 3 });
        await storage.ReplaceAsync("master", new byte[] { 7, 8, 9 });

        Assert.Equal(
            new byte[] { 7, 8, 9 },
            await storage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task ReplaceAsync_WhenEntryMissing_ThrowsKeyNotFoundException()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => storage.ReplaceAsync(
                "missing",
                new byte[] { 1 }).AsTask());
    }

    [Fact]
    public async Task DeleteAsync_IsIdempotent()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        await storage.CreateAsync("master", new byte[] { 1, 2, 3 });

        await storage.DeleteAsync("master");
        await storage.DeleteAsync("master");

        Assert.Null(await storage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_WithEmptyData_ThrowsArgumentException()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.CreateAsync(
                "master",
                ReadOnlyMemory<byte>.Empty).AsTask());
    }

    [Fact]
    public async Task CreateAsync_WithDataAboveLimit_ThrowsArgumentException()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(
            directory.Path,
            maximumEntrySizeBytes: 4);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.CreateAsync(
                "master",
                new byte[5]).AsTask());
    }

    [Fact]
    public async Task TryLoadAsync_WhenExistingFileAboveLimit_ThrowsInvalidDataException()
    {
        using var directory = new TemporaryDirectory();
        const string keyId = "master";

        var path = System.IO.Path.Combine(
            directory.Path,
            FileKeyNameEncoder.Encode(keyId) + ".key");

        File.WriteAllBytes(path, new byte[5]);

        var storage = CreateStorage(
            directory.Path,
            maximumEntrySizeBytes: 4);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => storage.TryLoadAsync(keyId).AsTask());
    }

    [Fact]
    public async Task LogicalIdentifier_IsNotUsedAsPhysicalFileName()
    {
        using var directory = new TemporaryDirectory();
        const string keyId = "service/auth/master";
        var storage = CreateStorage(directory.Path);

        await storage.CreateAsync(keyId, new byte[] { 1 });

        var files = Directory.GetFiles(directory.Path, "*.key");

        var file = Assert.Single(files);
        Assert.Equal(
            FileKeyNameEncoder.Encode(keyId) + ".key",
            System.IO.Path.GetFileName(file));
    }

    [Fact]
    public async Task CreateAsync_CanceledBeforeOperation_DoesNotCreateEntry()
    {
        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.CreateAsync(
                "master",
                new byte[] { 1 },
                cancellation.Token).AsTask());

        Assert.Null(await storage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_CanceledImmediatelyBeforeCommit_DoesNotPublishEntry()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();

        var hooks = new FileKeyStorageTestHooks
        {
            BeforeCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Create)
                {
                    cancellation.Cancel();
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.CreateAsync(
                "master",
                new byte[] { 1, 2, 3 },
                cancellation.Token).AsTask());

        Assert.Null(await CreateStorage(directory.Path).TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_CancellationAfterCommit_DoesNotTurnSuccessIntoCancellation()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();

        var hooks = new FileKeyStorageTestHooks
        {
            AfterCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Create)
                {
                    cancellation.Cancel();
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        var result = await storage.CreateAsync(
            "master",
            new byte[] { 1, 2, 3 },
            cancellation.Token);

        Assert.True(result);
        Assert.NotNull(await CreateStorage(directory.Path).TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_ExceptionBeforeCommit_LeavesNoPublishedEntry()
    {
        using var directory = new TemporaryDirectory();

        var hooks = new FileKeyStorageTestHooks
        {
            BeforeCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Create)
                {
                    throw new InvalidOperationException("Injected failure.");
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.CreateAsync(
                "master",
                new byte[] { 1, 2, 3 }).AsTask());

        Assert.Null(await CreateStorage(directory.Path).TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_ExceptionAfterCommit_IsIgnored()
    {
        using var directory = new TemporaryDirectory();

        var hooks = new FileKeyStorageTestHooks
        {
            AfterCommit = (_, _) =>
                throw new InvalidOperationException("Injected failure.")
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        Assert.True(
            await storage.CreateAsync(
                "master",
                new byte[] { 1, 2, 3 }));

        Assert.NotNull(await CreateStorage(directory.Path).TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_WhenFirstTemporaryNameCollides_RetriesWithAnotherName()
    {
        using var directory = new TemporaryDirectory();

        var occupied = System.IO.Path.Combine(
            directory.Path,
            ".occupied.tmp");

        File.WriteAllText(occupied, "occupied");

        var calls = 0;

        var hooks = new FileKeyStorageTestHooks
        {
            TemporaryFileNameFactory = () =>
                Interlocked.Increment(ref calls) == 1
                    ? ".occupied.tmp"
                    : ".free.tmp"
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        Assert.True(await storage.CreateAsync("master", new byte[] { 1 }));
        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task CreateAsync_WhenAllTemporaryNamesCollide_PreservesLastIOException()
    {
        using var directory = new TemporaryDirectory();

        var occupied = System.IO.Path.Combine(
            directory.Path,
            ".occupied.tmp");

        File.WriteAllText(occupied, "occupied");

        var hooks = new FileKeyStorageTestHooks
        {
            TemporaryFileNameFactory = () => ".occupied.tmp"
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        var exception = await Assert.ThrowsAsync<IOException>(
            () => storage.CreateAsync(
                "master",
                new byte[] { 1 }).AsTask());

        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public async Task DeleteAsync_CanceledBeforeCommit_LeavesEntryUntouched()
    {
        using var directory = new TemporaryDirectory();
        var normalStorage = CreateStorage(directory.Path);
        await normalStorage.CreateAsync("master", new byte[] { 1 });

        using var cancellation = new CancellationTokenSource();

        var hooks = new FileKeyStorageTestHooks
        {
            BeforeCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Delete)
                {
                    cancellation.Cancel();
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.DeleteAsync(
                "master",
                cancellation.Token).AsTask());

        Assert.NotNull(await normalStorage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task DeleteAsync_CancellationAfterCommit_DoesNotTurnSuccessIntoCancellation()
    {
        using var directory = new TemporaryDirectory();
        var normalStorage = CreateStorage(directory.Path);
        await normalStorage.CreateAsync("master", new byte[] { 1 });

        using var cancellation = new CancellationTokenSource();

        var hooks = new FileKeyStorageTestHooks
        {
            AfterCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Delete)
                {
                    cancellation.Cancel();
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await storage.DeleteAsync("master", cancellation.Token);

        Assert.Null(await normalStorage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_OnUnix_UsesRestrictedDirectoryAndFileModes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        await storage.CreateAsync("master", new byte[] { 1 });

        var expectedDirectoryMode =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute;

        var expectedFileMode =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite;

        Assert.Equal(
            expectedDirectoryMode,
            File.GetUnixFileMode(directory.Path));

        var file = Assert.Single(Directory.GetFiles(directory.Path, "*.key"));

        Assert.Equal(
            expectedFileMode,
            File.GetUnixFileMode(file));
    }


    [Fact]
    public async Task PublicOperations_WithInvalidKeyId_DoNotCreateStorageDirectory()
    {
        using var parent = new TemporaryDirectory();
        var storagePath = System.IO.Path.Combine(parent.Path, "keys");
        var storage = CreateStorage(storagePath);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.TryLoadAsync(" ").AsTask());

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.CreateAsync(" ", new byte[] { 1 }).AsTask());

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.ReplaceAsync(" ", new byte[] { 1 }).AsTask());

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.DeleteAsync(" ").AsTask());

        Assert.False(Directory.Exists(storagePath));
    }

    [Fact]
    public async Task TryLoadAsync_WithInvalidKeyId_OnUnix_DoesNotAdjustDirectoryPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var storage = CreateStorage(directory.Path);

        var broadMode =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute;

        File.SetUnixFileMode(directory.Path, broadMode);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.TryLoadAsync(" ").AsTask());

        Assert.Equal(
            broadMode,
            File.GetUnixFileMode(directory.Path));
    }

    [Fact]
    public async Task ReplaceAsync_CanceledImmediatelyBeforeCommit_PreservesOriginalEntry()
    {
        using var directory = new TemporaryDirectory();
        var normalStorage = CreateStorage(directory.Path);
        await normalStorage.CreateAsync("master", new byte[] { 1, 2, 3 });

        using var cancellation = new CancellationTokenSource();
        var hooks = new FileKeyStorageTestHooks
        {
            BeforeCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Replace)
                {
                    cancellation.Cancel();
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => storage.ReplaceAsync(
                "master",
                new byte[] { 7, 8, 9 },
                cancellation.Token).AsTask());

        Assert.Equal(
            new byte[] { 1, 2, 3 },
            await normalStorage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task ReplaceAsync_CancellationAfterCommit_DoesNotTurnSuccessIntoCancellation()
    {
        using var directory = new TemporaryDirectory();
        var normalStorage = CreateStorage(directory.Path);
        await normalStorage.CreateAsync("master", new byte[] { 1, 2, 3 });

        using var cancellation = new CancellationTokenSource();
        var hooks = new FileKeyStorageTestHooks
        {
            AfterCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Replace)
                {
                    cancellation.Cancel();
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await storage.ReplaceAsync(
            "master",
            new byte[] { 7, 8, 9 },
            cancellation.Token);

        Assert.Equal(
            new byte[] { 7, 8, 9 },
            await normalStorage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task ReplaceAsync_ExceptionBeforeCommit_PreservesOriginalEntry()
    {
        using var directory = new TemporaryDirectory();
        var normalStorage = CreateStorage(directory.Path);
        await normalStorage.CreateAsync("master", new byte[] { 1, 2, 3 });

        var hooks = new FileKeyStorageTestHooks
        {
            BeforeCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Replace)
                {
                    throw new InvalidOperationException("Injected failure.");
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.ReplaceAsync(
                "master",
                new byte[] { 7, 8, 9 }).AsTask());

        Assert.Equal(
            new byte[] { 1, 2, 3 },
            await normalStorage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task ReplaceAsync_ExceptionAfterCommit_IsIgnored()
    {
        using var directory = new TemporaryDirectory();
        var normalStorage = CreateStorage(directory.Path);
        await normalStorage.CreateAsync("master", new byte[] { 1, 2, 3 });

        var hooks = new FileKeyStorageTestHooks
        {
            AfterCommit = (operation, _) =>
            {
                if (operation == FileKeyStorageOperation.Replace)
                {
                    throw new InvalidOperationException("Injected failure.");
                }
            }
        };

        var storage = CreateStorage(directory.Path, hooks: hooks);

        await storage.ReplaceAsync(
            "master",
            new byte[] { 7, 8, 9 });

        Assert.Equal(
            new byte[] { 7, 8, 9 },
            await normalStorage.TryLoadAsync("master"));
    }

    [Fact]
    public async Task CreateAsync_FromTwoStorageInstances_OnlyOneCreatesEntry()
    {
        using var directory = new TemporaryDirectory();
        using var barrier = new Barrier(2);

        Action<FileKeyStorageOperation, string> synchronizeBeforeCommit =
            (operation, _) =>
            {
                if (operation != FileKeyStorageOperation.Create)
                {
                    return;
                }

                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException(
                        "Both concurrent operations failed to reach the commit boundary in time.");
                }
            };

        var firstStorage = CreateStorage(
            directory.Path,
            hooks: new FileKeyStorageTestHooks
            {
                BeforeCommit = synchronizeBeforeCommit
            });

        var secondStorage = CreateStorage(
            directory.Path,
            hooks: new FileKeyStorageTestHooks
            {
                BeforeCommit = synchronizeBeforeCommit
            });

        var firstData = new byte[] { 1, 1, 1 };
        var secondData = new byte[] { 2, 2, 2 };

        var firstTask = Task.Run(
            async () => await firstStorage.CreateAsync("race", firstData));

        var secondTask = Task.Run(
            async () => await secondStorage.CreateAsync("race", secondData));

        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, results.Count(static created => created));

        var stored = await CreateStorage(directory.Path).TryLoadAsync("race");
        Assert.NotNull(stored);

        var expected = results[0] ? firstData : secondData;
        Assert.Equal(expected, stored);
    }

    private static FileKeyStorage CreateStorage(
        string directoryPath,
        int maximumEntrySizeBytes = 64 * 1024,
        FileKeyStorageTestHooks? hooks = null)
    {
        var options = new FileKeyStorageOptions
        {
            DirectoryPath = directoryPath,
            MaximumEntrySizeBytes = maximumEntrySizeBytes
        };

        return hooks is null
            ? new FileKeyStorage(options)
            : new FileKeyStorage(options, hooks);
    }
}
