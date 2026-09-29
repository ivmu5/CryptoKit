namespace CryptoKit.Storage;

internal static class FileKeyStorageOptionsValidator
{
    internal static void Validate(FileKeyStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            throw new InvalidOperationException(
                "Путь к каталогу хранения ключей должен быть задан явно.");
        }

        if (options.MaximumEntrySizeBytes <= 0)
        {
            throw new InvalidOperationException(
                "Максимальный размер записи хранилища должен быть больше нуля.");
        }

        string directoryPath;

        // Проверяем возможность получить абсолютный путь заранее,
        // чтобы ошибка конфигурации обнаружилась при создании хранилища.
        try
        {
            directoryPath = Path.GetFullPath(options.DirectoryPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Путь к каталогу хранения ключей '{options.DirectoryPath}' некорректен.",
                exception);
        }

        if (File.Exists(directoryPath))
        {
            throw new InvalidOperationException(
                $"Путь к каталогу хранения ключей '{directoryPath}' указывает на файл.");
        }
    }
}
