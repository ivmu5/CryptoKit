using System.Security.Cryptography;

namespace CryptoKit.Rsa;

/// <summary>
/// Defines an asynchronous source of persisted RSA key pairs and derived public keys.
/// </summary>
public interface IRsaKeyProvider
{
    /// <summary>
    /// Loads an existing RSA key pair by its logical identifier.
    /// </summary>
    /// <param name="keyId">The logical RSA key-pair identifier.</param>
    /// <param name="cancellationToken">A token used to cancel storage waits.</param>
    /// <returns>
    /// A new caller-owned RSA key pair. The caller must dispose it after the private
    /// key material is no longer required.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// No RSA key pair exists for <paramref name="keyId"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Stored private-key material is malformed or violates the minimum RSA key-size policy.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled before completion.
    /// </exception>
    ValueTask<RsaKeyPair> GetKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an existing RSA key pair or atomically creates one when no record exists.
    /// </summary>
    /// <param name="keyId">The logical RSA key-pair identifier.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel storage waits or key creation before commit.
    /// </param>
    /// <returns>
    /// A new caller-owned RSA key pair. The caller must dispose it after use.
    /// </returns>
    /// <remarks>
    /// This method explicitly permits creation of new private-key material. Use
    /// <see cref="GetKeyPairAsync"/> when only previously provisioned material is acceptable.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Existing private-key material is malformed or violates the minimum RSA key-size policy.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Creation cannot converge because the same storage record is continuously
    /// changed by competing operations.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled before completion.
    /// </exception>
    ValueTask<RsaKeyPair> GetOrCreateKeyPairAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the public key derived from an existing persisted RSA private key without
    /// exposing private-key material to the caller.
    /// </summary>
    /// <param name="keyId">The logical RSA key-pair identifier.</param>
    /// <param name="cancellationToken">A token used to cancel storage waits.</param>
    /// <returns>
    /// A new caller-owned byte array containing the public key in SubjectPublicKeyInfo format.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// No RSA key pair exists for <paramref name="keyId"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Stored private-key material is malformed or violates the minimum RSA key-size policy.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled before completion.
    /// </exception>
    ValueTask<byte[]> GetPublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the public key for an existing RSA pair or creates and persists a new pair
    /// when no record exists.
    /// </summary>
    /// <param name="keyId">The logical RSA key-pair identifier.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel storage waits or key creation before commit.
    /// </param>
    /// <returns>
    /// A new caller-owned byte array containing the public key in SubjectPublicKeyInfo format.
    /// </returns>
    /// <remarks>
    /// When creation is required, only the private key is persisted. The private key is not
    /// returned by this method; the public key is derived from the persisted private material.
    /// </remarks>
    /// <exception cref="CryptographicException">
    /// Existing private-key material is malformed or violates the minimum RSA key-size policy.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Creation cannot converge because the same storage record is continuously
    /// changed by competing operations.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled before completion.
    /// </exception>
    ValueTask<byte[]> GetOrCreatePublicKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
