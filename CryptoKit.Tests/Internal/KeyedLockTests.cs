using CryptoKit.Internal;
using Xunit;

namespace CryptoKit.Tests.Internal;

public sealed class KeyedLockTests
{
    [Fact]
    public async Task AcquireAsync_SameKey_SerializesAccess()
    {
        var keyedLock = new KeyedLock();

        var first = await keyedLock.AcquireAsync("same");

        try
        {
            // Второй захват того же ключа должен ждать освобождения первого.
            var pending = keyedLock.AcquireAsync("same").AsTask();

            Assert.False(pending.IsCompleted);

            first.Dispose();

            using var second = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            first.Dispose();
        }
    }

    [Fact]
    public async Task AcquireAsync_DifferentKeys_DoNotBlockEachOther()
    {
        var keyedLock = new KeyedLock();

        using var first = await keyedLock.AcquireAsync("first");

        using var second = await keyedLock
            .AcquireAsync("second")
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AcquireAsync_CanceledWaiter_DoesNotBreakFutureAcquisition()
    {
        var keyedLock = new KeyedLock();
        var first = await keyedLock.AcquireAsync("same");

        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => keyedLock.AcquireAsync(
                    "same",
                    cancellation.Token).AsTask());
        }
        finally
        {
            first.Dispose();
        }

        // После отменённого waiter счётчик ссылок должен быть восстановлен,
        // и новый захват должен работать нормально.
        using var next = await keyedLock
            .AcquireAsync("same")
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Releaser_DisposeTwice_IsSafe()
    {
        var keyedLock = new KeyedLock();
        var releaser = await keyedLock.AcquireAsync("key");

        releaser.Dispose();
        releaser.Dispose();

        using var next = await keyedLock.AcquireAsync("key");
    }
}
