// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace NineDigit.SerialTransport;

public static class TransportExtensions
{
    public static Task WriteOneAsync(this ITransport self, byte data, CancellationToken cancellationToken)
    {
        if (self is null)
            throw new ArgumentNullException(nameof(self));

        return self.WriteAsync([data], cancellationToken);
    }
        
    public static async Task<byte> ReadOneAsync(this ITransport self, CancellationToken cancellationToken)
    {
        if (self is null)
            throw new ArgumentNullException(nameof(self));

        var buffer = await self.ReadAsync(responseLength: 1, cancellationToken).ConfigureAwait(false);
        return buffer[0];
    }

    /// <summary>
    /// </summary>
    /// <param name="self">Transport</param>
    /// <param name="buffer">The buffer to write the data into.</param>
    /// <param name="offset">The byte offset in buffer at which to begin writing data from the stream.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The value of the TResult
    /// parameter contains the total number of bytes read into the buffer.
    /// </returns>
    public static async Task<int> ReadAsync(this ITransport self, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (self is null)
            throw new ArgumentNullException(nameof(self));

        var bytes = await self.ReadAsync(count, cancellationToken).ConfigureAwait(false);
        bytes.CopyTo(buffer, offset);
        return bytes.Length;
    }
}