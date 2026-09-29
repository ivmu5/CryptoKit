namespace CryptoKit.Storage;

/// <summary>
/// Defines an asynchronous, algorithm-agnostic storage contract for binary
/// cryptographic key material.
/// </summary>
/// <remarks>
/// <para>
/// The storage does not depend on a particular algorithm and can hold RSA, AES,
/// HMAC, or other key material.
/// </para>
/// <para>
/// Create and replace semantics are intentionally separate. Implementations must
/// provide atomic create-only behavior and must not emulate it with a prior existence check.
/// </para>
/// <para>
/// The asynchronous contract allows backends such as file systems, secure stores,
/// keychains, or remote KMS implementations without introducing synchronous wrappers.
/// </para>
/// </remarks>
public interface IKeyStorage
{
    /// <summary>
    /// Attempts to load a record by its logical storage identifier.
    /// </summary>
    /// <param name="keyId">The unique storage identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// A new caller-owned byte array containing the record, or <see langword="null"/>
    /// when the record does not exist.
    /// </returns>
    /// <remarks>
    /// If the returned array contains secret material, the caller is responsible for
    /// clearing it after use.
    /// </remarks>
    ValueTask<byte[]?> TryLoadAsync(
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically creates a new record only when no record with the same identifier exists.
    /// </summary>
    /// <param name="keyId">The unique storage identifier.</param>
    /// <param name="data">
    /// The record bytes. The implementation must not retain a reference to caller-owned memory
    /// after the operation completes.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the operation before commit.</param>
    /// <returns>
    /// <see langword="true"/> when this operation created the record; otherwise
    /// <see langword="false"/> when a record already existed and was left unchanged.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Absence detection and creation must form one atomic storage operation. Calling
    /// <see cref="TryLoadAsync"/> first is not a correctness mechanism for create-only semantics.
    /// </para>
    /// <para>
    /// Cancellation may be honored only before the irreversible storage change. Once a record
    /// has been committed, an implementation must not report cancellation solely because the
    /// token is canceled afterward.
    /// </para>
    /// </remarks>
    ValueTask<bool> CreateAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces an existing record.
    /// </summary>
    /// <param name="keyId">The unique storage identifier.</param>
    /// <param name="data">
    /// The replacement bytes. The implementation must not retain a reference to caller-owned
    /// memory after the operation completes.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the operation before commit.</param>
    /// <remarks>
    /// <para>
    /// The operation must never create a missing record. A missing destination results in
    /// <see cref="KeyNotFoundException"/>.
    /// </para>
    /// <para>
    /// After a successful replacement, the implementation must not report cancellation in a
    /// way that would falsely imply the replacement did not occur.
    /// </para>
    /// </remarks>
    /// <exception cref="KeyNotFoundException">
    /// No record exists for <paramref name="keyId"/>.
    /// </exception>
    ValueTask ReplaceAsync(
        string keyId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a record by its identifier.
    /// </summary>
    /// <param name="keyId">The unique storage identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation before commit.</param>
    /// <remarks>
    /// <para>
    /// Delete is idempotent: a missing record is treated as a successful result.
    /// </para>
    /// <para>
    /// Deleting a storage record does not guarantee physical erasure of previous bytes from
    /// the underlying medium, file-system snapshots, or backups.
    /// </para>
    /// <para>
    /// Once deletion has occurred, an implementation must not report cancellation solely
    /// because the token is canceled afterward.
    /// </para>
    /// </remarks>
    ValueTask DeleteAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
