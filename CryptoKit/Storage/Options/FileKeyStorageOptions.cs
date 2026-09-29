namespace CryptoKit.Storage;

/// <summary>
/// Configures the file-backed cryptographic key storage.
/// </summary>
public sealed class FileKeyStorageOptions
{
    /// <summary>
    /// Gets or sets the directory that stores key files.
    /// </summary>
    /// <remarks>
    /// The application must configure this path explicitly; CryptoKit does not choose a default.
    /// </remarks>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum size, in bytes, of one storage record.
    /// </summary>
    /// <remarks>
    /// The limit is enforced for both reads and writes. The default is 64 KiB per record.
    /// </remarks>
    public int MaximumEntrySizeBytes { get; set; } = 64 * 1024;
}
