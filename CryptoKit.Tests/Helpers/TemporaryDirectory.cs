namespace CryptoKit.Tests.Helpers;

/// <summary>
/// Provides an isolated temporary directory for file-system tests.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// Creates a new unique directory under the process temporary path.
    /// </summary>
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "CryptoKit.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Gets the temporary directory path.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// Best-effort deletes the temporary directory and all of its contents.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Test-directory cleanup must not hide the result of the test itself.
        }
    }
}
