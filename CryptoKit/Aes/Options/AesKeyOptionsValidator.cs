namespace CryptoKit.Aes;

/// <summary>
/// Validates AES key-generation options and AES key sizes.
/// </summary>
internal static class AesKeyOptionsValidator
{
    /// <summary>
    /// Validates a complete AES options object.
    /// </summary>
    internal static void Validate(AesKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateKeySize(
            options.KeySize,
            nameof(options.KeySize));
    }

    /// <summary>
    /// Validates that a key size is one of the AES sizes supported by CryptoKit.
    /// </summary>
    internal static void ValidateKeySize(
        int keySize,
        string? parameterName = null)
    {
        if (keySize is not (128 or 192 or 256))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                "AES key size must be 128, 192, or 256 bits.");
        }
    }
}
