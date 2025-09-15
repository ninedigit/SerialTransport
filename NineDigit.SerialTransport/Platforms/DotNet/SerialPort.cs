#if DESKTOP
using System.IO.Ports;
// ReSharper disable CheckNamespace

namespace NineDigit.SerialTransport
{
    internal class SerialPort : ISerialPort
    {
        public event EventHandler<SerialPortErrorEventArgs>? OnError;

        private readonly System.IO.Ports.SerialPort _serialPort;

        /// <summary>
        /// </summary>
        /// <param name="portName">Názov sériového portu, napríklad COM1 alebo /dev/ttyS0.</param>
        /// <param name="options">Communication options.</param>
        public SerialPort(string portName, SerialPortOptions options)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("Invalid serial port name.", nameof(portName));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            if (options.BaudRate <= 0)
                throw new ArgumentException("Baud rate must be an positive number.", nameof(options));

            if (options.ReadTimeout.TotalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.ReadTimeout)} must be an positive non-zero number.");
            
            if (options.ReadTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.ReadTimeout)} must be less than {int.MaxValue}.");

            if (options.WriteTimeout.TotalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.WriteTimeout)} must be an positive non-zero number.");
            
            if (options.WriteTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.WriteTimeout)} must be less than {int.MaxValue}.");

            var parity = GetParity(options.Parity);
            var stopBits = GetStopBits(options.StopBits);
            
            _serialPort = new System.IO.Ports.SerialPort(portName, options.BaudRate, parity, options.DataBits, stopBits)
            {
                WriteTimeout = (int)options.WriteTimeout.TotalMilliseconds,
                ReadTimeout = (int)options.ReadTimeout.TotalMilliseconds
            };
            
            _serialPort.ErrorReceived += OnSerialPortErrorReceived;
        }

        public string Name => _serialPort.PortName;

        public int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }

        private void OnSerialPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            var code = ((int)e.EventType).ToString();
            var errorType = typeof(System.IO.Ports.SerialError).FullName;
            var args = new SerialPortErrorEventArgs(code, errorType, message: null, exception: null);
            
            OnError?.Invoke(this, args);
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePortIsOpen();
            return Task.CompletedTask;
        }

        private void EnsurePortIsOpen()
        {
            try
            {
                if (_serialPort.IsOpen)
                    return;
                
                _serialPort.Open();
                DiscardBuffers();
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new BusyPortException(_serialPort.PortName, $"Port '{_serialPort.PortName}' is busy.", ex);
            }
            catch (Exception ex) // System.IO.FileNotFoundException
            {
                throw new TransportException(_serialPort.PortName, $"Unable to connect to the device connected at {_serialPort.PortName}.", ex);
            }
        }

        public void Write(byte[] data)
        {
            try
            {
                EnsurePortIsOpen();
                var stream = _serialPort.BaseStream;

                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TransportException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WriteTransportException(_serialPort.PortName, "Failed to write data to the port.", ex);
            }
        }

        public byte[] Read(int responseLength)
        {
            var stream = _serialPort.BaseStream;
            var result = new byte[responseLength];
            var bytesToRead = responseLength;
            var totalBytesRead = 0;

            while (bytesToRead > 0)
            {
                var bytesRead = stream.Read(result, totalBytesRead, bytesToRead);
                if (bytesRead == 0)
                    break;

                bytesToRead -= bytesRead;
                totalBytesRead += bytesRead;
            }

            if (totalBytesRead != responseLength)
                throw new InvalidOperationException($"Incomplete message received. Expected bytes: {responseLength}, received bytes: {totalBytesRead}.");

            return result;
        }

        public void DiscardBuffers()
        {
            if (!_serialPort.IsOpen)
                return;
            
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
        }

        public void Close()
        {
            if (_serialPort.IsOpen)
                _serialPort.Close();
        }

        #region IDisposable
        private bool _disposed;

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            
            if (disposing)
            {
                try
                {
                    _serialPort.ErrorReceived -= OnSerialPortErrorReceived;
                    _serialPort.Dispose();
                }
                // "Port does not exists" occurs if device has been plugged out physically.
                catch (IOException) { }
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
        
        private System.IO.Ports.Parity GetParity(Parity parity)
        {
            return parity switch
            {
                Parity.None => System.IO.Ports.Parity.None,
                Parity.Odd => System.IO.Ports.Parity.Odd,
                Parity.Even => System.IO.Ports.Parity.Even,
                Parity.Mark => System.IO.Ports.Parity.Mark,
                Parity.Space => System.IO.Ports.Parity.Space,
                _ => throw new NotSupportedException($"Parity {parity} is not supported.")
            };
        }

        private System.IO.Ports.StopBits GetStopBits(StopBits stopBits)
        {
            return stopBits switch
            {
                StopBits.One => System.IO.Ports.StopBits.One,
                StopBits.OnePointFive => System.IO.Ports.StopBits.OnePointFive,
                StopBits.Two => System.IO.Ports.StopBits.Two,
                _ => throw new NotSupportedException($"StopBit {stopBits} is not supported.")
            };
        }
    }
}
#endif