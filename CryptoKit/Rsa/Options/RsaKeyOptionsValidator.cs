using System.Security.Cryptography;

namespace CryptoKit.Rsa;

internal static class RsaKeyOptionsValidator
{
    internal const int MinimumKeySize = 2048;

    internal static void Validate(RsaKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateKeySize(
            options.KeySize,
            nameof(options.KeySize));
    }

    internal static void ValidateKeySize(
        int keySize,
        string? parameterName = null)
    {
        if (!MeetsMinimumKeySize(keySize))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"Размер RSA-ключа не может быть меньше {MinimumKeySize} бит.");
        }

        if (!IsSupportedByCurrentImplementation(keySize))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"Размер RSA-ключа {keySize} бит не поддерживается " +
                "текущей реализацией RSA.");
        }
    }

    internal static bool MeetsMinimumKeySize(int keySize)
    {
        return keySize >= MinimumKeySize;
    }

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

            // SkipSize == 0 означает единственное допустимое значение MinSize.
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
