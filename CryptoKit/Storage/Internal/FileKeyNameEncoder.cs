using System.Security.Cryptography;
using System.Text;
using CryptoKit.Internal;

namespace CryptoKit.Storage;

/// <summary>
/// Converts logical key identifiers into deterministic file-system-safe names.
/// </summary>
internal static class FileKeyNameEncoder
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    /// <summary>
    /// Returns the uppercase hexadecimal SHA-256 hash of the strict UTF-8 key identifier.
    /// </summary>
    internal static string Encode(string keyId)
    {
        KeyIdValidator.Validate(keyId);

        var encodedKeyId = StrictUtf8.GetBytes(keyId);
        var hash = SHA256.HashData(encodedKeyId);

        return Convert.ToHexString(hash);
    }
}
