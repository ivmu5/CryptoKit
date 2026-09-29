using System.Security.Cryptography;
using CryptoKit.Storage;

namespace CryptoKit.Tests.Helpers;

/// <summary>
/// Thread-safe in-memory IKeyStorage implementation used by unit tests to exercise
/// key providers without touching the file system.
/// </summary>
internal sealed class InMemoryKeyStorage : IKeyStorage, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, byte[]> _entries =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<byte[]?> TryLoadAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return ValueTask.FromResult(
                _entries.TryGetValue(keyId, out var value)
                    ? value.ToArray()
                    : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> CreateAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var copy = data.ToArray();

        lock (_syncRoot)
        {
            if (_entries.ContainsKey(keyId))
            {
                CryptographicOperations.ZeroMemory(copy);
                return ValueTask.FromResult(false);
            }

            _entries.Add(keyId, copy);
            return ValueTask.FromResult(true);
        }
    }

    /// <inheritdoc />
    public ValueTask ReplaceAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var replacement = data.ToArray();

        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(keyId, out var current))
            {
                CryptographicOperations.ZeroMemory(replacement);
                throw new KeyNotFoundException();
            }

            _entries[keyId] = replacement;
            CryptographicOperations.ZeroMemory(current);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (_entries.Remove(keyId, out var value))
            {
                CryptographicOperations.ZeroMemory(value);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Replaces the only stored record with arbitrary bytes so provider behavior can
    /// be tested against corrupted key material.
    /// </summary>
    internal void ReplaceOnlyEntry(ReadOnlySpan<byte> data)
    {
        lock (_syncRoot)
        {
            Assert.Single(_entries);

            var key = _entries.Keys.Single();
            var current = _entries[key];

            _entries[key] = data.ToArray();
            CryptographicOperations.ZeroMemory(current);
        }
    }

    /// <summary>
    /// Gets the number of records currently held by the fake storage.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Clears all retained key buffers and removes every in-memory record.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            foreach (var value in _entries.Values)
            {
                CryptographicOperations.ZeroMemory(value);
            }

            _entries.Clear();
        }
    }
}
