namespace CryptoKit.Internal;

internal static class KeyIdValidator
{
    internal static void Validate(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException(
                "Идентификатор ключа не может быть пустым.",
                nameof(keyId));
        }

        ValidateUtf16(keyId, nameof(keyId));
    }

    private static void ValidateUtf16(
        string value,
        string parameterName)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length ||
                    !char.IsLowSurrogate(value[index + 1]))
                {
                    throw CreateInvalidUnicodeException(parameterName);
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                throw CreateInvalidUnicodeException(parameterName);
            }
        }
    }

    private static ArgumentException CreateInvalidUnicodeException(
        string parameterName)
    {
        return new ArgumentException(
            "Идентификатор ключа должен содержать корректную UTF-16 последовательность.",
            parameterName);
    }
}
