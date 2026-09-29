using System.Runtime.Versioning;
using System.Security.Cryptography;
using CryptoKit.Internal;

namespace CryptoKit.Storage;

/// <summary>
/// Реализует асинхронное хранение криптографического ключевого материала
/// в файловой системе.
/// </summary>
/// <remarks>
/// <para>
/// Хранилище не различает алгоритмы и работает с данными как с бинарными
/// записями. На Unix каталог хранения принудительно ограничивается правами
/// владельца <c>rwx------</c>, а файлы записей — <c>rw-------</c>.
/// На остальных платформах дополнительная защита каталога и файлов
/// обеспечивается средствами операционной системы.
/// </para>
/// <para>
/// Логический идентификатор записи не используется как имя файла напрямую.
/// Имя файла формируется как SHA-256 от UTF-8 представления идентификатора,
/// что обеспечивает одинаковое отображение на поддерживаемых платформах.
/// </para>
/// <para>
/// Создание и замена выполняются через временный файл в том же каталоге.
/// Атомарное создание только при отсутствии записи обеспечивается вызовом
/// <see cref="File.Move(string,string,bool)"/> с запрещённой заменой, без
/// предварительной проверки существования и без локальной блокировки процесса.
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
    /// Создаёт файловое хранилище ключей.
    /// </summary>
    /// <param name="options">Настройки файлового хранилища.</param>
    public FileKeyStorage(FileKeyStorageOptions options)
        : this(options, testHooks: null)
    {
    }

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
                    $"Размер записи ключа '{keyId}' ({length} байт) превышает " +
                    $"допустимый предел {_maximumEntrySizeBytes} байт.");
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

            // Закрываем файловый дескриптор до атомарной публикации файла.
            // На Windows переименование открытого файла может завершиться ошибкой.
            await temporaryFile.Stream
                .DisposeAsync()
                .ConfigureAwait(false);
            streamDisposed = true;

            InvokeBeforeCommit(
                FileKeyStorageOperation.Create,
                keyId);

            // Последняя проверка отмены перед необратимой публикацией записи.
            cancellationToken.ThrowIfCancellationRequested();

            var created = TryPublishCreateOnly(
                temporaryFile.Path,
                path,
                cancellationToken);

            if (!created)
            {
                return false;
            }

            // После успешного File.Move основная запись уже опубликована.
            // Последующая отмена или диагностические тестовые обработчики
            // не должны менять результат операции.
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

            // Закрываем файловый дескриптор до атомарной замены файла.
            // На Windows замена открытого файла может завершиться ошибкой.
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
                // File.Replace требует существующий целевой файл и поэтому
                // не превращает замену в неявное создание записи.
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

        // File.Delete идемпотентен для отсутствующего файла, поэтому
        // отдельная проверка File.Exists не требуется.

        // Тестовый обработчик вызывается до последней проверки отмены, чтобы
        // тесты могли детерминированно проверить границу необратимого удаления.
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
            // Отсутствующий каталог эквивалентен отсутствующей записи.
        }

        // После File.Delete больше не проверяем токен отмены: файл уже мог быть
        // удалён, и OperationCanceledException исказил бы фактический результат.
        InvokeAfterCommitBestEffort(
            FileKeyStorageOperation.Delete,
            keyId);

        return ValueTask.CompletedTask;
    }

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
                // File.Move использует IOException как для конфликта имени,
                // так и для других ошибок ввода-вывода. Если целевая запись
                // всё ещё существует, другой участник выиграл создание.
                if (File.Exists(destinationPath))
                {
                    EnsureExistingKeyFilePermissions(destinationPath);
                    return false;
                }

                // Запись могла существовать в момент File.Move, но быть удалена
                // до проверки File.Exists. В этом случае ограниченно повторяем
                // ту же атомарную публикацию. Реальная постоянная I/O-ошибка
                // после нескольких попыток не скрывается и выходит наружу.
                if (retryCount >= MaximumCreatePublishRetries)
                {
                    throw;
                }

                retryCount++;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

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
                // FileMode.CreateNew сообщает IOException как при коллизии имени,
                // так и при некоторых других ошибках ввода-вывода. Проверка
                // File.Exists после исключения сама была бы подвержена гонке:
                // конкурирующий процесс может удалить файл до этой проверки.
                //
                // Поэтому ограниченно повторяем попытку с новым именем для любого
                // IOException. Постоянная ошибка не скрывается: после исчерпания
                // попыток последнее исключение сохраняется как InnerException.
                lastIOException = exception;
            }
        }

        throw new IOException(
            "Не удалось создать временный файл для записи ключа.",
            lastIOException);
    }

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
            // Ошибка вспомогательной очистки не должна скрывать исходный результат.
        }
    }

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
            // Каталог мог быть удалён конкурентной операцией между
            // Directory.Exists и чтением или изменением его Unix-прав.
        }
    }

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
            // Запись могла отсутствовать изначально или быть удалена
            // конкурентной операцией между проверкой прав и их изменением.
        }
    }

    private void InvokeBeforeCommit(
        FileKeyStorageOperation operation,
        string keyId)
    {
        _testHooks?.BeforeCommit?.Invoke(
            operation,
            keyId);
    }

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
            // После изменения основной записи ошибка тестового обработчика
            // не должна менять результат уже завершённой операции.
        }
    }

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            _testHooks?.BeforeTemporaryCleanup?.Invoke(path);
            File.Delete(path);
        }
        catch (Exception)
        {
            // Ошибка вспомогательной очистки не должна скрывать уже известный результат.
        }
    }

    private string GetKeyPath(string keyId)
    {
        var fileName = FileKeyNameEncoder.Encode(keyId);

        return Path.Combine(
            _directoryPath,
            fileName + FileExtension);
    }

    private void ValidateData(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException(
                "Данные ключа не могут быть пустыми.",
                nameof(data));
        }

        if (data.Length > _maximumEntrySizeBytes)
        {
            throw new ArgumentException(
                $"Размер данных ключа ({data.Length} байт) превышает " +
                $"допустимый предел {_maximumEntrySizeBytes} байт.",
                nameof(data));
        }
    }

    private static KeyNotFoundException CreateMissingKeyException(
        string keyId,
        Exception innerException)
    {
        return new KeyNotFoundException(
            $"Ключ с идентификатором '{keyId}' не найден.",
            innerException);
    }

    [UnsupportedOSPlatformGuard("windows")]
    private static bool SupportsUnixFileMode()
    {
        return !OperatingSystem.IsWindows();
    }

    private readonly record struct TemporaryFile(
        string Path,
        FileStream Stream);
}

internal sealed class FileKeyStorageTestHooks
{
    internal Func<string>? TemporaryFileNameFactory { get; init; }

    internal Action<FileKeyStorageOperation, string>? BeforeCommit { get; init; }

    internal Action<FileKeyStorageOperation, string>? AfterCommit { get; init; }

    internal Action<string>? BeforeTemporaryCleanup { get; init; }
}

internal enum FileKeyStorageOperation
{
    Create,
    Replace,
    Delete
}
