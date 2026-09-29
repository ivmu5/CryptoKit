namespace CryptoKit.Internal;

/// <summary>
/// Provides asynchronous per-key mutual exclusion while allowing unrelated keys
/// to proceed independently.
/// </summary>
/// <remarks>
/// Lock entries are reference-counted and removed after the final owner or waiter
/// releases its reference, preventing the dictionary from growing indefinitely.
/// </remarks>
internal sealed class KeyedLock
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, LockEntry> _entries;

    /// <summary>
    /// Creates a keyed lock using the supplied key comparer.
    /// </summary>
    internal KeyedLock(IEqualityComparer<string>? comparer = null)
    {
        _entries = new Dictionary<string, LockEntry>(
            comparer ?? EqualityComparer<string>.Default);
    }

    /// <summary>
    /// Acquires the asynchronous lock associated with <paramref name="key"/>.
    /// </summary>
    /// <returns>
    /// A disposable lease that releases the keyed lock when disposed.
    /// </returns>
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

            // Count both the current owner and queued waiters. The entry must remain alive
            // while any operation still references its semaphore.
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

    /// <summary>
    /// Releases the semaphore and the lease's reference to its lock entry.
    /// </summary>
    private void Release(
        string key,
        LockEntry entry)
    {
        entry.Semaphore.Release();

        ReleaseReference(
            key,
            entry);
    }

    /// <summary>
    /// Removes and disposes an unused lock entry after its final reference is released.
    /// </summary>
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

    /// <summary>
    /// Holds the semaphore and reference count for one logical key.
    /// </summary>
    private sealed class LockEntry
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);

        internal int ReferenceCount { get; set; }
    }

    /// <summary>
    /// Represents one idempotent lease over a keyed lock entry.
    /// </summary>
    private sealed class Releaser : IDisposable
    {
        private readonly string _key;
        private readonly LockEntry _entry;

        private KeyedLock? _owner;

        /// <summary>
        /// Creates a lease bound to one keyed lock entry.
        /// </summary>
        internal Releaser(
            KeyedLock owner,
            string key,
            LockEntry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        /// <summary>
        /// Releases the lease once. Repeated calls are safe.
        /// </summary>
        public void Dispose()
        {
            // Interlocked.Exchange makes disposal idempotent even if callers invoke it twice.
            var owner = Interlocked.Exchange(
                ref _owner,
                null);

            owner?.Release(
                _key,
                _entry);
        }
    }
}
