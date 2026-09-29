using System.Security.Cryptography;

namespace CryptoKit.Rsa;

/// <summary>
/// Validates RSA key sizes against CryptoKit policy and the current platform implementation.
/// </summary>
internal static class RsaKeyOptionsValidator
{
    internal const int MinimumKeySize = 2048;

    /// <summary>
    /// Validates a complete RSA options object.
    /// </summary>
    internal static void Validate(RsaKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateKeySize(
            options.KeySize,
            nameof(options.KeySize));
    }

    /// <summary>
    /// Validates that a requested RSA size meets CryptoKit policy and is supported
    /// by the active RSA implementation.
    /// </summary>
    internal static void ValidateKeySize(
        int keySize,
        string? parameterName = null)
    {
        if (!MeetsMinimumKeySize(keySize))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"RSA key size cannot be less than {MinimumKeySize} bits.");
        }

        if (!IsSupportedByCurrentImplementation(keySize))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"RSA key size {keySize} bits is not supported " +
                "by the current RSA implementation.");
        }
    }

    /// <summary>
    /// Returns whether a key size meets CryptoKit's minimum RSA security policy.
    /// </summary>
    internal static bool MeetsMinimumKeySize(int keySize)
    {
        return keySize >= MinimumKeySize;
    }

    /// <summary>
    /// Checks the legal RSA key-size ranges exposed by the active platform implementation.
    /// </summary>
    private static bool IsSupportedByCurrentImplementation(int keySize)
    {
        using var rsa = RSA.Create();

        foreach (var legalKeySize in rsa.LegalKeySizes)
        {
            if (keySize < legalKeySize.MinSize ||
                keySize > legalKeySize.MaxSize)
            {
                continue;
            }

            // SkipSize == 0 means that MinSize is the only legal value in this range.
            if (legalKeySize.SkipSize == 0)
            {
                return keySize == legalKeySize.MinSize;
            }

            if ((keySize - legalKeySize.MinSize) % legalKeySize.SkipSize == 0)
            {
                return true;
            }
        }

        return false;
    }
}
