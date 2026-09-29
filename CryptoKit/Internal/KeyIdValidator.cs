namespace CryptoKit.Internal;

/// <summary>
/// Validates logical key identifiers before they are passed to providers or storage backends.
/// </summary>
internal static class KeyIdValidator
{
    /// <summary>
    /// Ensures that a key identifier is non-empty and contains a valid UTF-16 sequence.
    /// </summary>
    internal static void Validate(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException(
                "Key identifier cannot be empty or whitespace.",
                nameof(keyId));
        }

        ValidateUtf16(keyId, nameof(keyId));
    }

    /// <summary>
    /// Rejects unpaired UTF-16 surrogate code units so later UTF-8 encoding is deterministic.
    /// </summary>
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

    /// <summary>
    /// Creates the consistent validation exception used for malformed UTF-16 input.
    /// </summary>
    private static ArgumentException CreateInvalidUnicodeException(
        string parameterName)
    {
        return new ArgumentException(
            "Key identifier must contain a valid UTF-16 sequence.",
            parameterName);
    }
}
