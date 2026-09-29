namespace CryptoKit.Hmac;

internal static class HmacKeyOptionsValidator
{
    internal const int MinimumKeySize = 128;

    // Ограничение относится только к генерации новых ключей и защищает
    // от ошибочной конфигурации, способной привести к чрезмерному выделению памяти.
    internal const int MaximumGeneratedKeySize = 65_536;

    internal static void Validate(HmacKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ValidateGeneratedKeySize(
            options.KeySize,
            nameof(options.KeySize));
    }

    internal static void ValidateKeyMaterialSize(
        int keySize,
        string? parameterName = null)
    {
        if (keySize < MinimumKeySize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                $"Размер HMAC-ключа не может быть меньше {MinimumKeySize} бит.");
        }

        if (keySize % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                keySize,
                "Размер HMAC-ключа должен быть кратен восьми битам.");
        }
    }

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
                $"Размер создаваемого HMAC-ключа не может превышать {MaximumGeneratedKeySize} бит.");
        }
    }
}
