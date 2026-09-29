namespace CryptoKit.Storage;

/// <summary>
/// Представляет настройки файлового хранилища криптографических ключей.
/// </summary>
public sealed class FileKeyStorageOptions
{
    /// <summary>
    /// Каталог, в котором будут храниться файлы ключей.
    /// Значение должно быть задано приложением явно.
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Максимальный размер одной записи хранилища в байтах.
    /// </summary>
    /// <remarks>
    /// Ограничение применяется как при записи, так и при чтении.
    /// По умолчанию допускается не более 64 КиБ на одну запись.
    /// </remarks>
    public int MaximumEntrySizeBytes { get; set; } = 64 * 1024;
}
