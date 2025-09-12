#if ANDROID
using Android.Content;
using Android.Hardware.Usb;
using Microsoft.Extensions.Logging;
using Hoho.Android.UsbSerial.Drivers;

namespace NineDigit.SerialTransport
{
    /// <summary>
    /// https://github.com/mik3y/usb-serial-for-android/blob/master/usbSerialForAndroid/src/main/java/com/hoho/android/usbserial/driver/UsbSerialPort.java
    /// </summary>
    public class SerialPort : ISerialPort
    {
        public event EventHandler<EventArgs>? OnError;

        private readonly UsbDeviceDetachedReceiver _detachedReceiver;
        private readonly IntentFilter _usbDeviceDetachedIntentFilter;
        private readonly Intent _usbDeviceDetachedIntent;
        
        private readonly UsbSerialPort _serialPort;
        private readonly UsbManager _usbManager;
        // private readonly IUsbPermissionService _usbPermissionService;
        private readonly int _baudRate;
        private readonly int _dataBits;
        private readonly Hoho.Android.UsbSerial.Parity _parity;
        private readonly Hoho.Android.UsbSerial.StopBits _stopBits;
        private readonly ILogger _logger;
        private readonly int _vendorId;
        private readonly int _productId;
        
        private UsbDeviceConnection? _connection;
        private bool _isOpen;
        
        public SerialPort(
            UsbSerialPort serialPort,
            UsbManager usbManager,
            // IUsbPermissionService usbPermissionService,
            SerialPortOptions options,
            ILogger<SerialPort> logger)
        {
            ArgumentNullException.ThrowIfNull(serialPort);
            ArgumentNullException.ThrowIfNull(usbManager);
            // ArgumentNullException.ThrowIfNull(usbPermissionService);
            ArgumentNullException.ThrowIfNull(options);

            _detachedReceiver = new UsbDeviceDetachedReceiver(OnUsbDeviceDetached);
            _usbDeviceDetachedIntentFilter = new IntentFilter(UsbManager.ActionUsbDeviceDetached);
            _usbDeviceDetachedIntent = Application.Context.RegisterReceiver(_detachedReceiver, _usbDeviceDetachedIntentFilter);

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

            ReadTimeout = (int)options.ReadTimeout.TotalMilliseconds;
            WriteTimeout = (int)options.WriteTimeout.TotalMilliseconds;
            
            _serialPort = serialPort;
            _usbManager = usbManager;
            // _usbPermissionService = usbPermissionService;
            
            _baudRate = options.BaudRate;
            _parity = GetParity(options.Parity);
            _dataBits = options.DataBits;
            _stopBits = GetStopBits(options.StopBits);
            
            _vendorId = _serialPort.Driver.Device.VendorId;
            _productId = _serialPort.Driver.Device.ProductId;
            
            _logger = logger;
        }

        private void OnUsbDeviceDetached(UsbDevice device)
        {
            if (device.DeviceName == _serialPort.Driver.Device.DeviceName)
            {
                _logger.LogDebug("USB Serial Port device {DeviceName} was detached", device.DeviceName);
                
                Close();
                OnError?.Invoke(this, EventArgs.Empty);
            }
        }

        public string Name => _serialPort.Driver.Device.DeviceName;
        
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (_isOpen)
                return;
            
            var deviceName = _serialPort.Driver.Device.DeviceName;
            
            // var permissionGranted = await _usbPermissionService
            //     .EnsureDevicePermissionAsync(_vendorId, _productId, cancellationToken)
            //     .ConfigureAwait(false);
            
            var permissionGranted = await _usbManager
                .RequestPermissionAsync(_serialPort.Driver.Device, Application.Context, cancellationToken)
                .ConfigureAwait(false);

            if (!permissionGranted)
                throw new UnauthorizedAccessException($"USB permission not granted for device {deviceName}");
            
            _logger.LogDebug("Opening USB Serial Port device with name {DeviceName}", deviceName);

            var connection = _usbManager.OpenDevice(_serialPort.Driver.Device);
            if (connection is null)
                throw new InvalidOperationException($"Failed to open device {deviceName}.");

            try
            {
                _logger.LogDebug("Opening USB Serial Port connection for device {DeviceName}", deviceName);
                _serialPort.Open(connection);
                
                _isOpen = true;
                _connection = connection;

                _serialPort.SetParameters(_baudRate, _dataBits, _stopBits, _parity);
                DiscardBuffers();
            }
            catch (Exception)
            {
                _isOpen = false;
                connection.Dispose();
                _connection = null;
                throw;
            }
        }

        public void Write(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var serialPort = EnsureSerialPort();

            _logger.LogDebug(
                "Writing {BytesCound} bytes of data to Serial Port with number {SerialPortNumber}",
                data.Length, serialPort.PortNumber);

            serialPort.Write(data, WriteTimeout);
        }

        public byte[] Read(int responseLength)
        {
            var serialPort = EnsureSerialPort();

            _logger.LogDebug(
               "Reading {BytesCount} bytes from Serial Port with number {SerialPortNumber}",
               responseLength, serialPort.PortNumber);

            return serialPort.Read(responseLength, ReadTimeout, _logger);
        }

        public void DiscardBuffers()
        {
            if (_isOpen)
                return;

            _logger.LogDebug("Discarding Serial Port buffers");

            //port?.PurgeHwBuffers(
            //    true,   // discard non-transmitted output data
            //    true);  // to discard non-read input data
        }

        public void Close()
        {
            if (!_isOpen)
                return;
            
            _logger.LogDebug("Closing Serial Port");

            _isOpen = false;
            
            _connection?.Close();
            _connection = null;
            
            _serialPort.Close();
        }

        private UsbSerialPort EnsureSerialPort()
        {
            var port = _serialPort;
            if (port is null)
                throw new InvalidOperationException("Port is not open.");

            return port;
        }

        #region IDisposable
        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            
            if (disposing)
            {
                Application.Context.UnregisterReceiver(_detachedReceiver);
                
                _usbDeviceDetachedIntent.Dispose();
                _usbDeviceDetachedIntentFilter.Dispose();
                _detachedReceiver.Dispose();
                
                _logger.LogDebug("Disposing Serial Port");
                Close();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
        
        private static Hoho.Android.UsbSerial.Parity GetParity(Parity parity)
        {
            return parity switch
            {
                Parity.None => Hoho.Android.UsbSerial.Parity.None,
                Parity.Odd => Hoho.Android.UsbSerial.Parity.Odd,
                Parity.Even => Hoho.Android.UsbSerial.Parity.Even,
                Parity.Mark => Hoho.Android.UsbSerial.Parity.Mark,
                Parity.Space => Hoho.Android.UsbSerial.Parity.Space,
                _ => throw new NotSupportedException($"Parity {parity} is not supported.")
            };
        }

        private static Hoho.Android.UsbSerial.StopBits GetStopBits(StopBits stopBits)
        {
            return stopBits switch
            {
                StopBits.One => Hoho.Android.UsbSerial.StopBits.One,
                StopBits.OnePointFive => Hoho.Android.UsbSerial.StopBits.OnePointFive,
                StopBits.Two => Hoho.Android.UsbSerial.StopBits.Two,
                _ => throw new NotSupportedException($"StopBit {stopBits} is not supported.")
            };
        }
        
        private class UsbDeviceDetachedReceiver : BroadcastReceiver
        {
            private readonly Action<UsbDevice> _onDetachedHandler;

            public UsbDeviceDetachedReceiver(Action<UsbDevice> onDetachedHandler)
            {
                _onDetachedHandler = onDetachedHandler;
            }

            public override void OnReceive(Context? context, Intent? intent)
            {
                if (intent?.GetParcelableExtra(UsbManager.ExtraDevice) is UsbDevice usbDevice)
                    _onDetachedHandler.Invoke(usbDevice);
            }
        }
    }
}
#endif