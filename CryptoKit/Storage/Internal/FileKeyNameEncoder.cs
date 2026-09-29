using System.Security.Cryptography;
using System.Text;
using CryptoKit.Internal;

namespace CryptoKit.Storage;

internal static class FileKeyNameEncoder
{
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    internal static string Encode(string keyId)
    {
        KeyIdValidator.Validate(keyId);

        var encodedKeyId = StrictUtf8.GetBytes(keyId);
        var hash = SHA256.HashData(encodedKeyId);

        return Convert.ToHexString(hash);
    }
}
