namespace CryptoKit.Storage;

/// <summary>
/// Validates file-storage configuration before a storage instance is created.
/// </summary>
internal static class FileKeyStorageOptionsValidator
{
    /// <summary>
    /// Validates the directory path and record-size limit.
    /// </summary>
    internal static void Validate(FileKeyStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            throw new InvalidOperationException(
                "Key storage directory path must be configured explicitly.");
        }

        if (options.MaximumEntrySizeBytes <= 0)
        {
            throw new InvalidOperationException(
                "Maximum key storage record size must be greater than zero.");
        }

        string directoryPath;

        // Resolve the absolute path up front so invalid configuration fails when the
        // storage is constructed rather than during the first storage operation.
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
                $"Key storage directory path '{options.DirectoryPath}' is invalid.",
                exception);
        }

        if (File.Exists(directoryPath))
        {
            throw new InvalidOperationException(
                $"Key storage directory path '{directoryPath}' points to a file.");
        }
    }
}
