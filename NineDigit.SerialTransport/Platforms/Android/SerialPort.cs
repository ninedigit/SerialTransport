#if ANDROID
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Hoho.Android.UsbSerial.Drivers;
// ReSharper disable CheckNamespace
// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace NineDigit.SerialTransport
{
    public class SerialPort : ISerialPort
    {
        public event EventHandler<SerialPortErrorEventArgs>? Error;
        
        private readonly UsbDeviceDetachedReceiver _detachedReceiver;
        private readonly IntentFilter _usbDeviceDetachedIntentFilter;
        private readonly Intent? _usbDeviceDetachedIntent;
        
        private readonly UsbSerialPort _serialPort;
        private readonly UsbManager _usbManager;
        private readonly int _baudRate;
        private readonly int _dataBits;
        private readonly Hoho.Android.UsbSerial.Parity _parity;
        private readonly Hoho.Android.UsbSerial.StopBits _stopBits;
        
        private UsbDeviceConnection? _connection;
        private bool _isOpen;
        
        public SerialPort(
            UsbSerialPort serialPort,
            UsbManager usbManager,
            SerialPortOptions options)
        {
            ArgumentNullException.ThrowIfNull(serialPort);
            ArgumentNullException.ThrowIfNull(usbManager);
            ArgumentNullException.ThrowIfNull(options);

            _detachedReceiver = new UsbDeviceDetachedReceiver(OnUsbDeviceDetached);
            _usbDeviceDetachedIntentFilter = new IntentFilter(UsbManager.ActionUsbDeviceDetached);

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                _usbDeviceDetachedIntent = Application.Context
                    .RegisterReceiver(_detachedReceiver, _usbDeviceDetachedIntentFilter, ReceiverFlags.NotExported);
            }
            else
            {
                _usbDeviceDetachedIntent = Application.Context
                    .RegisterReceiver(_detachedReceiver, _usbDeviceDetachedIntentFilter);
            }

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
            
            _baudRate = options.BaudRate;
            _parity = GetParity(options.Parity);
            _dataBits = options.DataBits;
            _stopBits = GetStopBits(options.StopBits);
        }

        private void OnUsbDeviceDetached(UsbDevice device)
        {
            if (device.DeviceName == _serialPort.Driver.Device.DeviceName)
            {
                SerialTransportEventSource.Log.SerialPortDeviceWasDetached(device.DeviceName);
                Close();
                
                Error?.Invoke(this, new SerialPortErrorEventArgs(message: $"Device {device.DeviceName} was detached"));
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
            
            var permissionGranted = await _usbManager
                .RequestPermissionAsync(_serialPort.Driver.Device, Application.Context, cancellationToken)
                .ConfigureAwait(false);

            if (!permissionGranted)
                throw new UnauthorizedAccessException($"USB permission not granted for device {deviceName}");
            
            SerialTransportEventSource.Log.OpeningSerialPortDevice(deviceName);

            var connection = _usbManager.OpenDevice(_serialPort.Driver.Device);
            if (connection is null)
                throw new InvalidOperationException($"Failed to open device {deviceName}.");

            try
            {
                SerialTransportEventSource.Log.OpeningSerialPortDeviceConnection(deviceName);
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

            SerialTransportEventSource.Log.WritingData(data.Length, _serialPort.Driver.Device.DeviceName);
            _serialPort.Write(data, WriteTimeout);
        }

        public byte[] Read(int responseLength)
        {
            SerialTransportEventSource.Log.ReadingData(responseLength, _serialPort.Driver.Device.DeviceName);
            return _serialPort.Read(responseLength, ReadTimeout);
        }

        public void DiscardBuffers()
        {
            if (_isOpen)
                return;

            SerialTransportEventSource.Log.DiscardingBuffers(_serialPort.Driver.Device.DeviceName);

            //port?.PurgeHwBuffers(
            //    true,   // discard non-transmitted output data
            //    true);  // to discard non-read input data
        }

        public void Close()
        {
            if (!_isOpen)
                return;
            
            SerialTransportEventSource.Log.ClosingSerialPortDeviceConnection(_serialPort.Driver.Device.DeviceName);

            _isOpen = false;
            
            _connection?.Close();
            _connection = null;
            
            _serialPort.Close();
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
                
                _usbDeviceDetachedIntent?.Dispose();
                _usbDeviceDetachedIntentFilter.Dispose();
                _detachedReceiver.Dispose();
                
                SerialTransportEventSource.Log.DisposingSerialPortDevice(_serialPort.Driver.Device.DeviceName);
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
        
        private class UsbDeviceDetachedReceiver(Action<UsbDevice> onDetachedHandler) : BroadcastReceiver
        {
            public override void OnReceive(Context? context, Intent? intent)
            {
                if (intent?.GetParcelableExtra(UsbManager.ExtraDevice) is UsbDevice usbDevice)
                    onDetachedHandler.Invoke(usbDevice);
            }
        }
    }
}
#endif