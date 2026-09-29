using System.Security.Cryptography;

namespace CryptoKit.Secrets;

/// <summary>
/// Базовый тип для секретного ключевого материала
/// с явным временем жизни и детерминированной очисткой собственного буфера.
/// </summary>
/// <remarks>
/// <para>
/// Экземпляр владеет собственной копией переданных данных.
/// Исходный буфер вызывающего кода не сохраняется.
/// </para>
/// <para>
/// После завершения работы экземпляр необходимо освободить через
/// <see cref="Dispose"/>. При этом собственный буфер немедленно очищается через
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>.
/// </para>
/// <para>
/// Если <see cref="Dispose"/> не был вызван, финализатор выполняет очистку как
/// резервную меру. Время запуска финализатора не гарантируется, поэтому он не
/// заменяет явное освобождение экземпляра.
/// </para>
/// <para>
/// Все операции, предоставляющие доступ к ключевому материалу после
/// освобождения экземпляра, выбрасывают <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public abstract class SecretKeyMaterial : IDisposable
{
    private readonly object _syncRoot = new();
    private byte[]? _buffer;

    private protected SecretKeyMaterial(byte[] ownedBuffer)
    {
        ArgumentNullException.ThrowIfNull(ownedBuffer);

        if (ownedBuffer.Length == 0)
        {
            throw new ArgumentException(
                "Секретный ключевой материал не может быть пустым.",
                nameof(ownedBuffer));
        }

        _buffer = ownedBuffer;
        Length = ownedBuffer.Length;
    }

    /// <summary>
    /// Размер секретного материала в байтах.
    /// </summary>
    /// <remarks>
    /// Метаданные размера остаются доступны после освобождения экземпляра.
    /// </remarks>
    public int Length { get; }

    /// <summary>
    /// Указывает, был ли собственный секретный буфер уничтожен.
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
    /// Копирует секретный материал в буфер вызывающего кода.
    /// </summary>
    /// <param name="destination">
    /// Буфер, размер которого должен быть не меньше <see cref="Length"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> слишком мал.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Экземпляр уже освобождён.
    /// </exception>
    public void CopyTo(Span<byte> destination)
    {
        lock (_syncRoot)
        {
            var buffer = GetBufferOrThrow();

            if (destination.Length < buffer.Length)
            {
                throw new ArgumentException(
                    $"Буфер назначения должен содержать не менее {buffer.Length} байт.",
                    nameof(destination));
            }

            buffer.AsSpan().CopyTo(destination);
        }
    }

    /// <summary>
    /// Создаёт принадлежащую вызывающему коду копию секретного материала.
    /// </summary>
    /// <returns>
    /// Новый массив байт. Вызывающий код становится владельцем массива
    /// и отвечает за его очистку после завершения работы.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Экземпляр уже освобождён.
    /// </exception>
    public byte[] Export()
    {
        lock (_syncRoot)
        {
            return GetBufferOrThrow().ToArray();
        }
    }

    /// <summary>
    /// Очищает собственный секретный буфер.
    /// Повторный вызов безопасен.
    /// </summary>
    public void Dispose()
    {
        ClearBuffer();
        GC.SuppressFinalize(this);
    }

    // Резервно очищает секретный буфер, если Dispose не был вызван явно.
    ~SecretKeyMaterial()
    {
        ClearBuffer();
    }

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

    private byte[] GetBufferOrThrow()
    {
        return _buffer ?? throw new ObjectDisposedException(GetType().FullName);
    }
}
