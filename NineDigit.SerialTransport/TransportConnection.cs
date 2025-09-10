using Microsoft.Extensions.Logging;

namespace NineDigit.SerialTransport
{
    /// <summary>
    /// Platform-independent implementation of serial transport
    /// </summary>
    internal class TransportConnection : TransportConnectionBase, ITransport
    {
        private readonly ISerialPort _serialPort;
        private readonly ILogger<TransportConnection> _logger;

        public TransportConnection(ISerialPort serialPort, ILogger<TransportConnection> logger)
        {
            _serialPort = serialPort
                ?? throw new ArgumentNullException(nameof(serialPort));

            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));

            _serialPort.OnError += SerialPort_OnError;
        }

        private void SerialPort_OnError(object? sender, EventArgs e)
        {
            _logger.LogError("Received Serial Port error event");
            _serialPort.Close();
            SetDisconnected();
        }

        public Task WriteAsync(byte[] data, CancellationToken cancellationToken)
            => WriteAndReadInternalAsync(data, responseLength: null, cancellationToken);

        public Task<byte[]> ReadAsync(long responseLength, CancellationToken cancellationToken)
            => WriteAndReadInternalAsync(null, responseLength, cancellationToken)!;

        public Task<byte[]> WriteAndReadAsync(byte[] data, long responseLength, CancellationToken cancellationToken)
            => WriteAndReadInternalAsync(data, responseLength, cancellationToken)!;

        private async Task<byte[]?> WriteAndReadInternalAsync(byte[]? data, long? responseLength, CancellationToken cancellationToken)
        {
            if (responseLength is < 0 or > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(responseLength));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _serialPort.OpenAsync(cancellationToken).ConfigureAwait(false);
                SetConnected();
            }
            catch (Exception ex)
            {
                SetDisconnected(ex);
                throw;
            }

            if (data != null)
            {
                try
                { 
                    cancellationToken.ThrowIfCancellationRequested();
                    _serialPort.Write(data);
                }
                catch (TransportException ex)
                {
                    SetDisconnected(ex);
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    SetDisconnected(ex);
                    throw new WriteTransportException(_serialPort.Name, $"Write operation to port '{_serialPort.Name}' was cancelled.", ex);
                }
                catch (TimeoutException ex)
                {
                    SetDisconnected(ex);
                    throw new WriteTimeoutException(_serialPort.WriteTimeout, _serialPort.Name, ex);
                }
                catch (Exception ex)
                {
                    SetDisconnected(ex);
                    throw new WriteTransportException($"An error has occured while writing to serial port '{_serialPort.Name}'.", ex);
                }
            }

            if (responseLength.HasValue)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return _serialPort.Read((int)responseLength.Value);
                }
                catch (TransportException ex)
                {
                    SetDisconnected(ex);
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    SetDisconnected(ex);
                    throw new ReadTransportException(_serialPort.Name, $"Write operation from port '{_serialPort.Name}' was cancelled.", ex);
                }
                catch (TimeoutException ex)
                {
                    SetDisconnected(ex);
                    throw new ReadTimeoutException(_serialPort.ReadTimeout, _serialPort.Name, ex);
                }
                catch (Exception ex)
                {
                    SetDisconnected(ex);
                    throw new ReadTransportException($"An error has occured while reading from serial port '{_serialPort.Name}'.", ex);
                }
            }

            return null;
        }

        public void DiscardBuffers()
            => _serialPort.DiscardBuffers();

        public void Disconnect(Exception ex)
        {
            _logger.LogDebug(ex, "Disconnecting with error");
            _serialPort.Close();
            SetDisconnected(ex);
        }
        
        #region IDisposable
        private bool _disposed;
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
                return;
            
            if (disposing)
            {
                _serialPort.OnError -= SerialPort_OnError;
                _serialPort?.Dispose();
            }

            _disposed = true;
        }
        #endregion
    }
}
