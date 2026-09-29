using System.Security.Cryptography;

namespace CryptoKit.Aes;

/// <summary>
/// Defines an asynchronous source of persisted AES keys.
/// </summary>
public interface IAesKeyProvider
{
    /// <summary>
    /// Loads an existing AES key by its logical identifier.
    /// </summary>
    /// <param name="keyId">The logical AES key identifier.</param>
    /// <param name="cancellationToken">A token used to cancel storage waits.</param>
    /// <returns>
    /// A new caller-owned instance containing the stored AES key material.
    /// The caller must dispose the returned key after use.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// No key exists for <paramref name="keyId"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Stored key material is invalid for AES.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled before completion.
    /// </exception>
    ValueTask<AesKey> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an existing AES key or atomically creates one when no record exists.
    /// </summary>
    /// <param name="keyId">The logical AES key identifier.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel storage waits or key creation before commit.
    /// </param>
    /// <returns>
    /// A new caller-owned AES key instance. The caller must dispose it after use.
    /// </returns>
    /// <remarks>
    /// This method explicitly permits creation of new secret key material. Use
    /// <see cref="GetKeyAsync"/> when only previously provisioned material is acceptable.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Existing stored material is invalid for AES.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Creation cannot converge because the same storage record is continuously
    /// changed by competing operations.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled before completion.
    /// </exception>
    ValueTask<AesKey> GetOrCreateKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
