using CryptoKit.Storage;

namespace CryptoKit.Tests.Helpers;

/// <summary>
/// Storage fake that always reports a lost CreateAsync race while subsequent loads
/// never find a winning record. It models pathological external create/delete churn.
/// </summary>
internal sealed class AlwaysContendedKeyStorage : IKeyStorage
{
    private int _createCallCount;

    /// <summary>
    /// Gets the number of create-only attempts observed by the fake.
    /// </summary>
    internal int CreateCallCount => Volatile.Read(ref _createCallCount);

    /// <inheritdoc />
    public ValueTask<byte[]?> TryLoadAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<byte[]?>(null);
    }

    /// <inheritdoc />
    public ValueTask<bool> CreateAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _createCallCount);
        return ValueTask.FromResult(false);
    }

    /// <inheritdoc />
    public ValueTask ReplaceAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
