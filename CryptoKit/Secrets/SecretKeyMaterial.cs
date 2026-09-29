using System.Security.Cryptography;

namespace CryptoKit.Secrets;

/// <summary>
/// Base type for secret key material with explicit ownership and deterministic
/// clearing of the owned buffer.
/// </summary>
/// <remarks>
/// <para>
/// The instance owns the supplied byte array and never exposes that buffer directly.
/// Public access is provided through copies or caller-provided destinations.
/// </para>
/// <para>
/// Call <see cref="Dispose"/> when the material is no longer needed. Disposal clears
/// the owned buffer immediately with <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>.
/// </para>
/// <para>
/// A finalizer performs best-effort clearing if disposal is omitted, but finalizer timing
/// is nondeterministic and is not a substitute for explicit disposal.
/// </para>
/// </remarks>
public abstract class SecretKeyMaterial : IDisposable
{
    private readonly object _syncRoot = new();
    private byte[]? _buffer;

    /// <summary>
    /// Initializes the instance with a buffer whose ownership is transferred to this object.
    /// </summary>
    /// <param name="ownedBuffer">The non-empty buffer that this instance will own and clear.</param>
    private protected SecretKeyMaterial(byte[] ownedBuffer)
    {
        ArgumentNullException.ThrowIfNull(ownedBuffer);

        if (ownedBuffer.Length == 0)
        {
            throw new ArgumentException(
                "Secret key material cannot be empty.",
                nameof(ownedBuffer));
        }

        _buffer = ownedBuffer;
        Length = ownedBuffer.Length;
    }

    /// <summary>
    /// Gets the length of the secret material in bytes.
    /// </summary>
    /// <remarks>
    /// Length metadata remains available after disposal.
    /// </remarks>
    public int Length { get; }

    /// <summary>
    /// Gets whether the owned secret buffer has been cleared and released.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_syncRoot)
            {
                return _buffer is null;
            }
        }
    }

    /// <summary>
    /// Copies the secret material into a caller-provided destination.
    /// </summary>
    /// <param name="destination">
    /// A destination buffer at least <see cref="Length"/> bytes long.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too small.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The instance has already been disposed.
    /// </exception>
    public void CopyTo(Span<byte> destination)
    {
        lock (_syncRoot)
        {
            var buffer = GetBufferOrThrow();

            if (destination.Length < buffer.Length)
            {
                throw new ArgumentException(
                    $"Destination buffer must contain at least {buffer.Length} bytes.",
                    nameof(destination));
            }

            buffer.AsSpan().CopyTo(destination);
        }
    }

    /// <summary>
    /// Exports a caller-owned copy of the secret material.
    /// </summary>
    /// <returns>
    /// A new byte array. The caller owns the returned copy and is responsible for
    /// clearing it after use.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// The instance has already been disposed.
    /// </exception>
    public byte[] Export()
    {
        lock (_syncRoot)
        {
            return GetBufferOrThrow().ToArray();
        }
    }

    /// <summary>
    /// Clears the owned secret buffer. Repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        ClearBuffer();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Provides best-effort clearing when callers fail to dispose the instance.
    /// </summary>
    ~SecretKeyMaterial()
    {
        ClearBuffer();
    }

    /// <summary>
    /// Clears and releases the owned buffer under synchronization.
    /// </summary>
    private void ClearBuffer()
    {
        lock (_syncRoot)
        {
            var buffer = _buffer;
            if (buffer is null)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(buffer);
            _buffer = null;
        }
    }

    /// <summary>
    /// Returns the owned buffer while the instance is alive.
    /// </summary>
    private byte[] GetBufferOrThrow()
    {
        return _buffer ?? throw new ObjectDisposedException(GetType().FullName);
    }
}
