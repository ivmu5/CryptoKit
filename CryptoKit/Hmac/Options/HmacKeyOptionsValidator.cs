namespace CryptoKit.Hmac;

/// <summary>
/// Validates HMAC key material and HMAC generation options.
/// </summary>
internal static class HmacKeyOptionsValidator
{
    internal const int MinimumKeySize = 128;

    // This limit applies only to newly generated keys and prevents accidental
    // configuration from causing excessive memory allocation.
    internal const int MaximumGeneratedKeySize = 65_536;

    /// <summary>
    /// Validates a complete HMAC options object.
    /// </summary>
    internal static void Validate(HmacKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateGeneratedKeySize(
            options.KeySize,
            nameof(options.KeySize));
    }

    /// <summary>
    /// Validates existing HMAC key material without applying the generation-only maximum.
    /// </summary>
    internal static void ValidateKeyMaterialSize(
        int keySize,
        string? parameterName = null)
    {
        if (keySize < MinimumKeySize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"HMAC key size cannot be less than {MinimumKeySize} bits.");
        }

        if (keySize % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                "HMAC key size must be divisible by eight bits.");
        }
    }

    /// <summary>
    /// Validates a requested size for newly generated HMAC key material.
    /// </summary>
    internal static void ValidateGeneratedKeySize(
        int keySize,
        string? parameterName = null)
    {
        ValidateKeyMaterialSize(keySize, parameterName);

        if (keySize > MaximumGeneratedKeySize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"Generated HMAC key size cannot exceed {MaximumGeneratedKeySize} bits.");
        }
    }
}
