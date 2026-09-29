using System.Security.Cryptography;
using CryptoKit.Secrets;

namespace CryptoKit.Rsa;

/// <summary>
/// Представляет пару RSA-ключей, принадлежащую вызывающему коду:
/// закрытый ключ и соответствующий ему открытый ключ.
/// </summary>
/// <remarks>
/// Закрытая часть хранится как собственный секретный материал и очищается при
/// <see cref="Dispose"/>. Открытая часть не является секретной,
/// но наружу также выдаётся только в виде копий, принадлежащих вызывающему коду.
/// </remarks>
public sealed class RsaKeyPair : IDisposable
{
    private readonly PrivateRsaKeyMaterial _privateKey;
    private readonly byte[] _publicKey;

    /// <summary>
    /// Создаёт объект, содержащий RSA-пару.
    /// </summary>
    /// <param name="privateKey">
    /// Закрытый RSA-ключ в формате PKCS#8.
    /// </param>
    /// <param name="publicKey">
    /// Открытый RSA-ключ в формате SubjectPublicKeyInfo.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Один из ключей пуст либо открытый ключ не соответствует закрытому.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Один из ключей имеет некорректный формат RSA либо размер закрытого
    /// ключа меньше минимально допустимого для CryptoKit.
    /// </exception>
    public RsaKeyPair(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> publicKey)
    {
        if (privateKey.IsEmpty)
        {
            throw new ArgumentException(
                "Закрытый RSA-ключ не может быть пустым.",
                nameof(privateKey));
        }

        if (publicKey.IsEmpty)
        {
            throw new ArgumentException(
                "Открытый RSA-ключ не может быть пустым.",
                nameof(publicKey));
        }

        ValidateKeyPair(privateKey, publicKey);

        var privateMaterial = new PrivateRsaKeyMaterial(privateKey);

        try
        {
            _publicKey = publicKey.ToArray();
            _privateKey = privateMaterial;
        }
        catch
        {
            privateMaterial.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Размер представления закрытого ключа в формате PKCS#8, в байтах.
    /// </summary>
    public int PrivateKeyLength => _privateKey.Length;

    /// <summary>
    /// Размер представления открытого ключа в формате SubjectPublicKeyInfo, в байтах.
    /// </summary>
    public int PublicKeyLength => _publicKey.Length;

    /// <summary>
    /// Указывает, уничтожена ли закрытая часть RSA-пары.
    /// </summary>
    public bool IsDisposed => _privateKey.IsDisposed;

    /// <summary>
    /// Копирует закрытый ключ в буфер вызывающего кода.
    /// </summary>
    /// <param name="destination">
    /// Буфер достаточного размера.
    /// </param>
    public void CopyPrivateKeyTo(Span<byte> destination)
    {
        _privateKey.CopyTo(destination);
    }

    /// <summary>
    /// Создаёт копию закрытого ключа, принадлежащую вызывающему коду.
    /// </summary>
    /// <returns>
    /// Новый массив байт с закрытым ключом в формате PKCS#8.
    /// Вызывающий код обязан очистить массив после использования.
    /// </returns>
    public byte[] ExportPrivateKey()
    {
        return _privateKey.Export();
    }

    /// <summary>
    /// Копирует открытый ключ в буфер вызывающего кода.
    /// </summary>
    /// <param name="destination">
    /// Буфер, размер которого должен быть не меньше <see cref="PublicKeyLength"/>.
    /// </param>
    public void CopyPublicKeyTo(Span<byte> destination)
    {
        if (destination.Length < _publicKey.Length)
        {
            throw new ArgumentException(
                $"Буфер назначения должен содержать не менее {_publicKey.Length} байт.",
                nameof(destination));
        }

        _publicKey.AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Создаёт копию открытого ключа, принадлежащую вызывающему коду.
    /// </summary>
    /// <returns>
    /// Новый массив байт с открытым ключом в формате SubjectPublicKeyInfo.
    /// </returns>
    public byte[] ExportPublicKey()
    {
        return _publicKey.ToArray();
    }

    /// <summary>
    /// Очищает собственный буфер закрытого ключа.
    /// Повторный вызов безопасен.
    /// </summary>
    public void Dispose()
    {
        _privateKey.Dispose();
    }

    // Проверяем форматы, минимальный размер и соответствие открытого ключа закрытому.
    private static void ValidateKeyPair(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> publicKey)
    {
        using var privateRsa = RSA.Create();
        using var publicRsa = RSA.Create();

        privateRsa.ImportPkcs8PrivateKey(
            privateKey,
            out var privateBytesRead);

        publicRsa.ImportSubjectPublicKeyInfo(
            publicKey,
            out var publicBytesRead);

        if (privateBytesRead != privateKey.Length)
        {
            throw new CryptographicException(
                "Закрытый RSA-ключ содержит лишние или некорректные данные.");
        }

        if (publicBytesRead != publicKey.Length)
        {
            throw new CryptographicException(
                "Открытый RSA-ключ содержит лишние или некорректные данные.");
        }

        // Политика минимального размера применяется не только при генерации,
        // но и к импортированному ключевому материалу. Иначе слабый сохранённый ключ
        // мог бы обойти ограничения CryptoKit через IKeyStorage или конструктор.
        if (!RsaKeyOptionsValidator.MeetsMinimumKeySize(privateRsa.KeySize))
        {
            throw new CryptographicException(
                $"Размер RSA-ключа не может быть меньше " +
                $"{RsaKeyOptionsValidator.MinimumKeySize} бит.");
        }

        var expectedPublicKey = privateRsa.ExportSubjectPublicKeyInfo();
        var actualPublicKey = publicRsa.ExportSubjectPublicKeyInfo();

        try
        {
            if (!expectedPublicKey
                .AsSpan()
                .SequenceEqual(actualPublicKey))
            {
                throw new ArgumentException(
                    "Открытый RSA-ключ не соответствует закрытому ключу.",
                    nameof(publicKey));
            }
        }
        finally
        {
            Array.Clear(expectedPublicKey);
            Array.Clear(actualPublicKey);
        }
    }

    private sealed class PrivateRsaKeyMaterial : SecretKeyMaterial
    {
        internal PrivateRsaKeyMaterial(ReadOnlySpan<byte> value)
            : base(value.ToArray())
        {
        }
    }
}
