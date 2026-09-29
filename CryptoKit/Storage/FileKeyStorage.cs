using System.Runtime.Versioning;
using System.Security.Cryptography;
using CryptoKit.Internal;

namespace CryptoKit.Storage;

/// <summary>
/// Stores cryptographic key material as binary files in a configured directory.
/// </summary>
/// <remarks>
/// <para>
/// The storage is algorithm-agnostic. On Unix-like platforms, the storage directory
/// is constrained to <c>rwx------</c> and key files to <c>rw-------</c>.
/// </para>
/// <para>
/// Logical identifiers are never used as file names directly. Each identifier is
/// encoded as the SHA-256 hash of its strict UTF-8 representation.
/// </para>
/// <para>
/// Create and replace operations write to a temporary file in the same directory
/// before publishing the result. Create-only publication uses
/// <see cref="File.Move(string,string,bool)"/> with overwrite disabled so correctness
/// does not depend on a prior existence check or an in-process lock.
/// </para>
/// </remarks>
public sealed class FileKeyStorage : IKeyStorage
{
    private const string FileExtension = ".key";
    private const int MaximumTemporaryFileAttempts = 32;
    private const int MaximumCreatePublishRetries = 2;

    private const UnixFileMode RequiredDirectoryUnixMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute;

    private const UnixFileMode RequiredFileUnixMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite;

    private readonly string _directoryPath;
    private readonly int _maximumEntrySizeBytes;
    private readonly FileKeyStorageTestHooks? _testHooks;

    /// <summary>
    /// Creates a file-backed key storage instance.
    /// </summary>
    /// <param name="options">The validated file-storage configuration.</param>
    public FileKeyStorage(FileKeyStorageOptions options)
        : this(options, testHooks: null)
    {
    }

    /// <summary>
    /// Creates a file-backed key storage instance with optional internal test hooks.
    /// </summary>
    internal FileKeyStorage(
        FileKeyStorageOptions options,
        FileKeyStorageTestHooks? testHooks)
    {
        ArgumentNullException.ThrowIfNull(options);

        FileKeyStorageOptionsValidator.Validate(options);

        _directoryPath = Path.GetFullPath(options.DirectoryPath);
        _maximumEntrySizeBytes = options.MaximumEntrySizeBytes;
        _testHooks = testHooks;

        EnsureExistingDirectoryPermissions();
    }

    /// <inheritdoc />
    public async ValueTask<byte[]?> TryLoadAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureExistingDirectoryPermissions();

        var path = GetKeyPath(keyId);
        EnsureExistingKeyFilePermissions(path);

        try
        {
            await using var stream = new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read | FileShare.Delete,
                    Options = FileOptions.Asynchronous |
                              FileOptions.SequentialScan
                });

            var length = stream.Length;

            if (length > _maximumEntrySizeBytes)
            {
                throw new InvalidDataException(
                    $"Key record '{keyId}' is {length} bytes, which exceeds " +
                    $"the configured limit of {_maximumEntrySizeBytes} bytes.");
            }

            var data = GC.AllocateUninitializedArray<byte>(
                checked((int)length));

            try
            {
                await stream
                    .ReadExactlyAsync(data, cancellationToken)
                    .ConfigureAwait(false);

                return data;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(data);
                throw;
            }
        }
        catch (Exception exception)
            when (exception is FileNotFoundException
                or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> CreateAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);
        ValidateData(data);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetKeyPath(keyId);
        EnsureDirectoryExists();
        EnsureExistingKeyFilePermissions(path);

        var temporaryFile = CreateTemporaryFile(cancellationToken);
        var streamDisposed = false;

        try
        {
            await WriteTemporaryFileAsync(
                    temporaryFile.Stream,
                    data,
                    cancellationToken)
                .ConfigureAwait(false);

            // Close the descriptor before atomic publication. Renaming an open file can
            // fail on Windows even when the same sequence succeeds on Unix-like systems.
            await temporaryFile.Stream
                .DisposeAsync()
                .ConfigureAwait(false);
            streamDisposed = true;

            InvokeBeforeCommit(
                FileKeyStorageOperation.Create,
                keyId);

            // This is the final cancellation boundary before irreversible publication.
            cancellationToken.ThrowIfCancellationRequested();

            var created = TryPublishCreateOnly(
                temporaryFile.Path,
                path,
                cancellationToken);

            if (!created)
            {
                return false;
            }

            // After File.Move succeeds, the record is already committed. Later cancellation
            // or diagnostic test hooks must not change the externally reported result.
            InvokeAfterCommitBestEffort(
                FileKeyStorageOperation.Create,
                keyId);

            return true;
        }
        finally
        {
            if (!streamDisposed)
            {
                await DisposeTemporaryStreamBestEffortAsync(
                        temporaryFile.Stream)
                    .ConfigureAwait(false);
            }

            TryDeleteTemporaryFile(temporaryFile.Path);
        }
    }

    /// <inheritdoc />
    public async ValueTask ReplaceAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);
        ValidateData(data);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetKeyPath(keyId);
        EnsureDirectoryExists();
        EnsureExistingKeyFilePermissions(path);

        var temporaryFile = CreateTemporaryFile(cancellationToken);
        var streamDisposed = false;

        try
        {
            await WriteTemporaryFileAsync(
                    temporaryFile.Stream,
                    data,
                    cancellationToken)
                .ConfigureAwait(false);

            // Close the descriptor before replacement for Windows compatibility.
            await temporaryFile.Stream
                .DisposeAsync()
                .ConfigureAwait(false);
            streamDisposed = true;

            InvokeBeforeCommit(
                FileKeyStorageOperation.Replace,
                keyId);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // File.Replace requires an existing destination and therefore cannot
                // accidentally turn replace semantics into create semantics.
                File.Replace(
                    temporaryFile.Path,
                    path,
                    destinationBackupFileName: null);
            }
            catch (FileNotFoundException exception)
            {
                throw CreateMissingKeyException(
                    keyId,
                    exception);
            }
            catch (DirectoryNotFoundException exception)
            {
                throw CreateMissingKeyException(
                    keyId,
                    exception);
            }

            InvokeAfterCommitBestEffort(
                FileKeyStorageOperation.Replace,
                keyId);
        }
        finally
        {
            if (!streamDisposed)
            {
                await DisposeTemporaryStreamBestEffortAsync(
                        temporaryFile.Stream)
                    .ConfigureAwait(false);
            }

            TryDeleteTemporaryFile(temporaryFile.Path);
        }
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        KeyIdValidator.Validate(keyId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureExistingDirectoryPermissions();

        var path = GetKeyPath(keyId);
        EnsureExistingKeyFilePermissions(path);

        // File.Delete is idempotent for a missing file, so no File.Exists pre-check is needed.

        // Invoke the hook before the last cancellation check so tests can deterministically
        // exercise the irreversible-delete boundary.
        InvokeBeforeCommit(
            FileKeyStorageOperation.Delete,
            keyId);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // A missing directory is equivalent to a missing record for delete semantics.
        }

        // Do not check cancellation after File.Delete: the record may already be gone, and
        // reporting cancellation would misrepresent the completed side effect.
        InvokeAfterCommitBestEffort(
            FileKeyStorageOperation.Delete,
            keyId);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Atomically publishes a temporary file only when the destination does not exist.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when this call published the record; otherwise
    /// <see langword="false"/> when another participant already owns the destination.
    /// </returns>
    private static bool TryPublishCreateOnly(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;

        while (true)
        {
            try
            {
                File.Move(
                    temporaryPath,
                    destinationPath,
                    overwrite: false);

                return true;
            }
            catch (IOException)
            {
                // File.Move reports IOException both for destination conflicts and for other
                // I/O failures. If the destination still exists, another participant won.
                if (File.Exists(destinationPath))
                {
                    EnsureExistingKeyFilePermissions(destinationPath);
                    return false;
                }

                // The destination may have existed when File.Move failed and then been deleted
                // before File.Exists ran. Retry the same atomic publish a bounded number of times;
                // a persistent I/O failure is rethrown instead of being hidden indefinitely.
                if (retryCount >= MaximumCreatePublishRetries)
                {
                    throw;
                }

                retryCount++;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    /// <summary>
    /// Creates a uniquely named temporary file in the storage directory.
    /// </summary>
    private TemporaryFile CreateTemporaryFile(
        CancellationToken cancellationToken)
    {
        IOException? lastIOException = null;

        for (var attempt = 0;
             attempt < MaximumTemporaryFileAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = _testHooks?.TemporaryFileNameFactory?.Invoke()
                ?? $".{Guid.NewGuid():N}.tmp";

            var path = Path.Combine(
                _directoryPath,
                fileName);

            try
            {
                return new TemporaryFile(
                    path,
                    new FileStream(
                        path,
                        CreateTemporaryFileOptions()));
            }
            catch (IOException exception)
            {
                // FileMode.CreateNew uses IOException for both name collisions and other I/O
                // failures. Classifying the exception with File.Exists would introduce another
                // race because the conflicting file could disappear before that check.
                //
                // Retry with another name for any IOException. If all attempts fail, preserve
                // the final IOException as the inner exception of the public failure.
                lastIOException = exception;
            }
        }

        throw new IOException(
            "Failed to create a temporary file for key storage.",
            lastIOException);
    }

    /// <summary>
    /// Builds the file options used for temporary key records.
    /// </summary>
    private static FileStreamOptions CreateTemporaryFileOptions()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous |
                      FileOptions.WriteThrough
        };

        if (SupportsUnixFileMode())
        {
            options.UnixCreateMode = RequiredFileUnixMode;
        }

        return options;
    }

    /// <summary>
    /// Disposes a temporary stream without allowing cleanup failure to mask the
    /// primary operation result.
    /// </summary>
    private static async ValueTask DisposeTemporaryStreamBestEffortAsync(
        FileStream stream)
    {
        try
        {
            await stream
                .DisposeAsync()
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort cleanup must not hide the original result or exception.
        }
    }

    /// <summary>
    /// Writes and flushes a complete temporary key record.
    /// </summary>
    private static async ValueTask WriteTemporaryFileAsync(
        FileStream stream,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        await stream
            .WriteAsync(data, cancellationToken)
            .ConfigureAwait(false);

        await stream
            .FlushAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the storage directory exists and applies the required Unix mode when supported.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        if (!SupportsUnixFileMode())
        {
            Directory.CreateDirectory(_directoryPath);
            return;
        }

        Directory.CreateDirectory(
            _directoryPath,
            RequiredDirectoryUnixMode);

        EnsureUnixDirectoryPermissions();
    }

    /// <summary>
    /// Reapplies required Unix permissions to an existing storage directory.
    /// </summary>
    private void EnsureExistingDirectoryPermissions()
    {
        if (!SupportsUnixFileMode() ||
            !Directory.Exists(_directoryPath))
        {
            return;
        }

        try
        {
            EnsureUnixDirectoryPermissions();
        }
        catch (Exception exception)
            when (exception is FileNotFoundException
                or DirectoryNotFoundException)
        {
            // A concurrent operation may remove the directory between Directory.Exists
            // and the permission read or write. Treat that disappearance as benign here.
        }
    }

    /// <summary>
    /// Sets the storage directory to the owner-only Unix mode required by CryptoKit.
    /// </summary>
    private void EnsureUnixDirectoryPermissions()
    {
        var currentMode = File.GetUnixFileMode(_directoryPath);

        if (currentMode == RequiredDirectoryUnixMode)
        {
            return;
        }

        File.SetUnixFileMode(
            _directoryPath,
            RequiredDirectoryUnixMode);
    }

    /// <summary>
    /// Reapplies owner-only Unix permissions to an existing key file when supported.
    /// </summary>
    private static void EnsureExistingKeyFilePermissions(string path)
    {
        if (!SupportsUnixFileMode())
        {
            return;
        }

        try
        {
            var currentMode = File.GetUnixFileMode(path);

            if (currentMode == RequiredFileUnixMode)
            {
                return;
            }

            File.SetUnixFileMode(
                path,
                RequiredFileUnixMode);
        }
        catch (Exception exception)
            when (exception is FileNotFoundException
                or DirectoryNotFoundException)
        {
            // The record may be absent or may disappear between permission inspection and update.
        }
    }

    /// <summary>
    /// Invokes the deterministic pre-commit test hook when configured.
    /// </summary>
    private void InvokeBeforeCommit(
        FileKeyStorageOperation operation,
        string keyId)
    {
        _testHooks?.BeforeCommit?.Invoke(
            operation,
            keyId);
    }

    /// <summary>
    /// Invokes the post-commit test hook without allowing diagnostic failures to
    /// change the result of an already committed storage operation.
    /// </summary>
    private void InvokeAfterCommitBestEffort(
        FileKeyStorageOperation operation,
        string keyId)
    {
        try
        {
            _testHooks?.AfterCommit?.Invoke(
                operation,
                keyId);
        }
        catch (Exception)
        {
            // The primary record has already changed; a test-hook failure must not rewrite history.
        }
    }

    /// <summary>
    /// Deletes a temporary file without masking the primary operation result.
    /// </summary>
    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            _testHooks?.BeforeTemporaryCleanup?.Invoke(path);
            File.Delete(path);
        }
        catch (Exception)
        {
            // Temporary cleanup is best effort once the main operation result is known.
        }
    }

    /// <summary>
    /// Maps a logical key identifier to its hashed file-system path.
    /// </summary>
    private string GetKeyPath(string keyId)
    {
        var fileName = FileKeyNameEncoder.Encode(keyId);

        return Path.Combine(
            _directoryPath,
            fileName + FileExtension);
    }

    /// <summary>
    /// Validates storage payload size before any file-system mutation occurs.
    /// </summary>
    private void ValidateData(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException(
                "Key data cannot be empty.",
                nameof(data));
        }

        if (data.Length > _maximumEntrySizeBytes)
        {
            throw new ArgumentException(
                $"Key data size ({data.Length} bytes) exceeds " +
                $"the configured limit of {_maximumEntrySizeBytes} bytes.",
                nameof(data));
        }
    }

    /// <summary>
    /// Creates the storage-level missing-record exception used by replace operations.
    /// </summary>
    private static KeyNotFoundException CreateMissingKeyException(
        string keyId,
        Exception innerException)
    {
        return new KeyNotFoundException(
            $"Key '{keyId}' was not found.",
            innerException);
    }

    /// <summary>
    /// Returns whether Unix file-mode APIs are applicable on the current platform.
    /// </summary>
    [UnsupportedOSPlatformGuard("windows")]
    private static bool SupportsUnixFileMode()
    {
        return !OperatingSystem.IsWindows();
    }

    /// <summary>
    /// Couples a temporary file path with its open stream during staged writes.
    /// </summary>
    private readonly record struct TemporaryFile(
        string Path,
        FileStream Stream);
}

/// <summary>
/// Internal hooks used to make commit boundaries and failure scenarios deterministic in tests.
/// </summary>
internal sealed class FileKeyStorageTestHooks
{
    internal Func<string>? TemporaryFileNameFactory { get; init; }

    internal Action<FileKeyStorageOperation, string>? BeforeCommit { get; init; }

    internal Action<FileKeyStorageOperation, string>? AfterCommit { get; init; }

    internal Action<string>? BeforeTemporaryCleanup { get; init; }
}

/// <summary>
/// Identifies the file-storage operation currently crossing a testable commit boundary.
/// </summary>
internal enum FileKeyStorageOperation
{
    Create,
    Replace,
    Delete
}
