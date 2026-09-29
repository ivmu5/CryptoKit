using CryptoKit.Storage;

namespace CryptoKit.Tests.Helpers;

/// <summary>
/// Хранилище, которое всегда сообщает, что CreateAsync проиграл гонку,
/// а последующая загрузка не находит победившую запись.
/// Имитирует патологический внешний create/delete churn.
/// </summary>
internal sealed class AlwaysContendedKeyStorage : IKeyStorage
{
    private int _createCallCount;

    internal int CreateCallCount => Volatile.Read(ref _createCallCount);

    public ValueTask<byte[]?> TryLoadAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<byte[]?>(null);
    }

    public ValueTask<bool> CreateAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _createCallCount);
        return ValueTask.FromResult(false);
    }

    public ValueTask ReplaceAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public ValueTask DeleteAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
