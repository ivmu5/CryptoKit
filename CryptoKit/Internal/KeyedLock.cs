namespace CryptoKit.Internal;

internal sealed class KeyedLock
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, LockEntry> _entries;

    internal KeyedLock(IEqualityComparer<string>? comparer = null)
    {
        _entries = new Dictionary<string, LockEntry>(
            comparer ?? EqualityComparer<string>.Default);
    }

    internal async ValueTask<IDisposable> AcquireAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        LockEntry entry;

        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new LockEntry();
                _entries.Add(key, entry);
            }

            // Учитываем как текущего владельца, так и ожидающие операции.
            // Пока счётчик больше нуля, запись блокировки нельзя удалить или освободить.
            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            return new Releaser(
                this,
                key,
                entry);
        }
        catch
        {
            ReleaseReference(
                key,
                entry);

            throw;
        }
    }

    private void Release(
        string key,
        LockEntry entry)
    {
        entry.Semaphore.Release();

        ReleaseReference(
            key,
            entry);
    }

    private void ReleaseReference(
        string key,
        LockEntry entry)
    {
        var shouldDispose = false;

        lock (_syncRoot)
        {
            entry.ReferenceCount--;

            if (entry.ReferenceCount != 0)
            {
                return;
            }

            if (_entries.TryGetValue(key, out var currentEntry) &&
                ReferenceEquals(currentEntry, entry))
            {
                _entries.Remove(key);
                shouldDispose = true;
            }
        }

        if (shouldDispose)
        {
            entry.Semaphore.Dispose();
        }
    }

    private sealed class LockEntry
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);

        internal int ReferenceCount { get; set; }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly string _key;
        private readonly LockEntry _entry;

        private KeyedLock? _owner;

        internal Releaser(
            KeyedLock owner,
            string key,
            LockEntry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        // Повторный вызов безопасен благодаря Interlocked.Exchange.
        public void Dispose()
        {
            var owner = Interlocked.Exchange(
                ref _owner,
                null);

            owner?.Release(
                _key,
                _entry);
        }
    }
}

