#if ANDROID
using System.Collections.Immutable;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial;
#endif

namespace NineDigit.SerialTransport;

/// <summary>
/// Platform-independent implementation of serial transport
/// </summary>
public class Transport : TransportConnectionBase, ITransport
{
    private readonly ISerialPort _serialPort;

    public Transport(ISerialPort serialPort)
    {
        _serialPort = serialPort
                      ?? throw new ArgumentNullException(nameof(serialPort));

        _serialPort.Error += SerialPort_OnError;
    }

    private void SerialPort_OnError(object? sender, SerialPortErrorEventArgs e)
    {
        SerialTransportEventSource.Log
            .ReceivedSerialPortError(_serialPort.Name, e.ErrorType, e.ErrorCode, e.Message, e.Exception?.ToString());
        
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
        SerialTransportEventSource.Log.SerialPortDeviceConnectionError(_serialPort.Name, ex.ToString());
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
            _serialPort.Error -= SerialPort_OnError;
            _serialPort.Dispose();
        }

        _disposed = true;
    }
    #endregion
    
#if DESKTOP
    public static ITransport Create(ISerialPortDeviceSelector serialPortDeviceSelector,
        SerialPortOptions options)
    {
        if (serialPortDeviceSelector is null)
            throw new ArgumentNullException(nameof(serialPortDeviceSelector));

        if (options is null)
            throw new ArgumentNullException(nameof(options));

        var serialPortManager = new NineDigit.SerialPort.SerialPortManager();

        IReadOnlyCollection<NineDigit.SerialPort.ISerialPortDevice> devices;

        try
        {
            devices = serialPortManager.GetSerialPortDevices();
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException)
        {
            // logger.LogWarning("Unable to get serial port devices on given platform");
            devices = [];
        }

        var serialPortDevices = devices.Select(device => new SerialPortDevice(device)).ToList();
        var selectedSerialPortDevice = serialPortDeviceSelector.SelectDevice(serialPortDevices);
            
        if (selectedSerialPortDevice is null)
            throw new InvalidOperationException("No serial port selected");

        var serialPort = new SerialPort(selectedSerialPortDevice.PortName, options);
        var transport = new Transport(serialPort);

        return transport;
    }
#elif ANDROID
    public static ITransport Create(
        UsbManager usbManager,
        ProbeTable probeTable,
        ISerialPortDeviceSelector serialPortDeviceSelector,
        SerialPortOptions options)
    {
        ArgumentNullException.ThrowIfNull(usbManager);
        ArgumentNullException.ThrowIfNull(serialPortDeviceSelector);
        ArgumentNullException.ThrowIfNull(options);
        
        var usbSerialProber = new UsbSerialProber(probeTable);
        var ports = usbSerialProber.FindAllDrivers(usbManager).SelectMany(i => i.Ports).ToImmutableList();

        if (ports.Count == 0)
            throw new InvalidOperationException("No compatible USB device was found.");

        var serialPortDevices = ports.Select(device => new SerialPortDevice(device)).ToList();
        var selectedSerialPortDevice = serialPortDeviceSelector.SelectDevice(serialPortDevices) as SerialPortDevice;
        
        if (selectedSerialPortDevice is null)
            throw new InvalidOperationException("No matching USB device was found.");

        var selectedSerialPort = selectedSerialPortDevice.GetUsbSerialPort();
        var serialPort = new SerialPort(selectedSerialPort, usbManager, options);
        var transport = new Transport(serialPort);

        return transport;
    }
#endif
}