#if ANDROID
using System.Security;
using Android.Content;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial;
using Microsoft.Extensions.Logging;
using Hoho.Android.UsbSerial.Drivers;

namespace NineDigit.SerialTransport
{
    /// <summary>
    /// https://github.com/mik3y/usb-serial-for-android/blob/master/usbSerialForAndroid/src/main/java/com/hoho/android/usbserial/driver/UsbSerialPort.java
    /// </summary>
    public class AndroidSerialPort : ISerialPort
    {
        public event EventHandler<EventArgs>? OnError;

        private Intent? _usbDeviceDetachedIntent;
        private IntentFilter? _usbDeviceDetachedIntentFilter;
        private UsbDeviceDetachedReceiver? _detachedReceiver;
        private bool _isOpen;

        private readonly UsbSerialPort _serialPort;
        private readonly UsbManager _usbManager;
        private readonly int _baudRate;
        private readonly int _dataBits;
        private readonly Hoho.Android.UsbSerial.Parity _parity;
        private readonly StopBits _stopBits;
        private readonly ILogger _logger;
        
        public AndroidSerialPort(
            UsbManager usbManager,
            UsbSerialPort serialPort,
            AndroidSerialPortOptions options,
            ILogger<AndroidSerialPort> logger)
        {
            ArgumentNullException.ThrowIfNull(usbManager);
            ArgumentNullException.ThrowIfNull(serialPort);
            ArgumentNullException.ThrowIfNull(options);

            if (options.ReadTimeout.TotalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Read timeout must be an positive non-zero number.");

            if (options.WriteTimeout.TotalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Write timeout must be an positive non-zero number.");

            ReadTimeout = (int)options.ReadTimeout.TotalMilliseconds;
            WriteTimeout = (int)options.WriteTimeout.TotalMilliseconds;
            
            _serialPort = serialPort;
            _usbManager = usbManager;
            _baudRate = options.BaudRate;
            _parity = options.Parity;
            _dataBits = options.DataBits;
            _stopBits = options.StopBits;
            _logger = logger;
        }

        public string Name => _serialPort.Driver.Device.DeviceName;
        
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (_isOpen)
                return;

            var permissionGranted = await _usbManager
                .RequestPermissionAsync(_serialPort.Driver.Device, Application.Context, cancellationToken)
                .ConfigureAwait(false);
                
            if (!permissionGranted)
                throw new SecurityException("This application does not have permission to use the this USB device.");

            var isOpen = _isOpen;
            if (!isOpen)
            {
                _logger.LogDebug("Opening USB Serial Port device with name '{DeviceName}' ...",
                    _serialPort.Driver.Device.DeviceName);

                var connection = _usbManager.OpenDevice(_serialPort.Driver.Device);
                if (connection is null)
                    throw new InvalidOperationException($"Failed to open device {_serialPort.Driver.Device.DeviceName}.");

                try
                {
                    _logger.LogDebug("Opening USB Serial Port connection ...");

                    _serialPort.Open(connection);

                    var detachedReceiver = _detachedReceiver = new UsbDeviceDetachedReceiver();
                    var intentFilter = _usbDeviceDetachedIntentFilter = new IntentFilter(UsbManager.ActionUsbDeviceDetached);

                    void OnUsbDeviceDetachedHandler(UsbDevice device)
                    {
                        if (device.DeviceName == _serialPort.Driver.Device.DeviceName)
                        {
                            _logger.LogDebug("USB Serial Port device '{DeviceName}' was detached", device.DeviceName);

                            detachedReceiver.Detached -= OnUsbDeviceDetachedHandler;
                            Close();
                            OnError?.Invoke(this, EventArgs.Empty);
                        }
                    }

                    detachedReceiver.Detached += OnUsbDeviceDetachedHandler;

                    _usbDeviceDetachedIntent = Application.Context.RegisterReceiver(detachedReceiver, intentFilter);
                    _isOpen = true;

                    _serialPort.SetParameters(_baudRate, _dataBits, _stopBits, _parity);
                    
                    DiscardBuffers();

                    // SetConnected();
                }
                catch (Exception)
                {
                    _detachedReceiver?.Dispose();
                    _usbDeviceDetachedIntentFilter?.Dispose();
                    _usbDeviceDetachedIntent?.Dispose();
                    _isOpen = false;

                    // SetDisconnected(ex);
                    throw;
                }
            }
        }

        public void Write(byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            var serialPort = EnsureSerialPort();

            _logger.LogDebug(
                "Writing {BytesCound} bytes of data to Serial Port with number {SerialPortNumber} ...",
                data.Length, serialPort.PortNumber);

            serialPort.Write(data, WriteTimeout);
        }

        public byte[] Read(int responseLength)
        {
            var serialPort = EnsureSerialPort();

            _logger.LogDebug(
               "Reading {BytesCount} bytes from Serial Port with number {SerialPortNumber} ...",
               responseLength, serialPort.PortNumber);

            return serialPort.Read(responseLength, ReadTimeout, _logger);
        }

        public void DiscardBuffers()
        {
            if (_isOpen)
                return;

            //logger.LogDebug("Discarding Serial Port buffers ...");

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

            _serialPort?.Close();

            _detachedReceiver?.Dispose();
            _detachedReceiver = null;

            _usbDeviceDetachedIntent?.Dispose();
            _usbDeviceDetachedIntent = null;

            _usbDeviceDetachedIntentFilter?.Dispose();
            _usbDeviceDetachedIntentFilter = null;
        }

        private UsbSerialPort EnsureSerialPort()
        {
            var port = _serialPort;
            if (port is null)
                throw new InvalidOperationException("Port is not open.");

            return port;
        }

        #region UsbDeviceDetachedReceiver implementation
        class UsbDeviceDetachedReceiver : BroadcastReceiver
        {
            public event Action<UsbDevice>? Detached;

            public UsbDeviceDetachedReceiver()
            { }

            public override void OnReceive(Context? context, Intent? intent)
            {
                if (intent is null)
                    return;
                
                if (intent.GetParcelableExtra(UsbManager.ExtraDevice) is UsbDevice usbDevice)
                    Detached?.Invoke(usbDevice);
            }
        }
        #endregion
        
        #region IDisposable
        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.LogDebug("Disposing Serial Port ...");

                    Close();
                    // _port?.Dispose();
                    _usbManager.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
#endif