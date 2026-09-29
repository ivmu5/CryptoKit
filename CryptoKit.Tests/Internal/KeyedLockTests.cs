using CryptoKit.Internal;
using Xunit;

namespace CryptoKit.Tests.Internal;

/// <summary>
/// Verifies per-key serialization, cancellation cleanup, and lease idempotency.
/// </summary>
public sealed class KeyedLockTests
{
    [Fact]
    public async Task AcquireAsync_SameKey_SerializesAccess()
    {
        var keyedLock = new KeyedLock();

        var first = await keyedLock.AcquireAsync("same");

        try
        {
            // A second lease for the same key must wait until the first lease is released.
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

        // A canceled waiter must release its reference so a later acquisition can proceed.
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
