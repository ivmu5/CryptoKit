namespace CryptoKit.Aes;

internal static class AesKeyOptionsValidator
{
    internal static void Validate(AesKeyOptions options)
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
        if (keySize is not (128 or 192 or 256))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                "Размер AES-ключа должен быть равен 128, 192 или 256 бит.");
        }
    }
}
