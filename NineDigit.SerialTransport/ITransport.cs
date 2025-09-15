// ReSharper disable UnusedMember.Global

namespace NineDigit.SerialTransport;

/// <summary>
/// Interface for transport layer that exchanges byte array between two endpoints.
/// </summary>
public interface ITransport : ITransportConnection, IDisposable
{
    /// <summary>
    /// Executes data exchange with another device.
    /// </summary>
    /// <param name="data">Data to be written.</param>
    /// <param name="responseLength">Expected response length (in bytes)</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response received from opposite endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> contains no elements.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="responseLength"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Operation was canceled.</exception>
    /// <exception cref="TransportException">Error during writing/reading data.</exception>
    Task<byte[]> WriteAndReadAsync(byte[] data, long responseLength, CancellationToken cancellationToken);
        
    Task WriteAsync(byte[] data, CancellationToken cancellationToken);        
        
    Task<byte[]> ReadAsync(long responseLength, CancellationToken cancellationToken);

    /// <summary>
    /// Discards input and output buffer.
    /// </summary>
    void DiscardBuffers();

    /// <summary>
    /// Disconnects from the other endpoint.
    /// </summary>
    /// <param name="ex">Error that casuses the disconnection.</param>
    void Disconnect(Exception ex);
}