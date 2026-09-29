namespace CryptoKit.Internal;

/// <summary>
/// Defines the bounded retry policy used when create-only key publication races
/// with competing writers or deleters.
/// </summary>
internal static class KeyCreationRetryPolicy
{
    // A normal create race resolves after loading the winning record once. The bound
    // prevents an unbounded loop when another participant continuously creates and
    // removes the same record between CreateAsync and the following load.
    internal const int MaximumAttempts = 8;

    /// <summary>
    /// Creates the exception reported after all atomic publication attempts are exhausted.
    /// </summary>
    internal static InvalidOperationException CreateExhaustedException(
        string keyDescription,
        string keyId)
    {
        return new InvalidOperationException(
            $"Failed to complete creation of key '{keyId}' " +
            $"(type: {keyDescription}) after {MaximumAttempts} atomic attempts. " +
            "The storage record is being continuously changed by competing operations.");
    }
}
