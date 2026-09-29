using System.Security.Cryptography;
using CryptoKit.Storage;

namespace CryptoKit.Tests.Helpers;

/// <summary>
/// Простая потокобезопасная реализация IKeyStorage только для unit-тестов.
/// Она позволяет проверять провайдеры ключей без обращения к файловой системе.
/// </summary>
internal sealed class InMemoryKeyStorage : IKeyStorage, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, byte[]> _entries =
        new(StringComparer.Ordinal);

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
    /// Заменяет единственную запись хранилища произвольными данными.
    /// Удобно для проверки поведения провайдеров при повреждённом key material.
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
