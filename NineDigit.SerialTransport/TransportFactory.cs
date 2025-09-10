#if ANDROID
using System.Collections.Immutable;
using Android.Content;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial;
using Hoho.Android.UsbSerial.Drivers;
#endif
using Microsoft.Extensions.Logging;

namespace NineDigit.SerialTransport;

/// <summary>
/// Helper for instantiating transport instance.
/// </summary>
public static class TransportFactory
{
    public static async Task<ITransport> CreateSerialTransportAsync(SerialPortDeviceSelectorDelegate serialPortSelector,
        SerialPortOptions options, ILoggerFactory loggerFactory, CancellationToken cancellationToken = default)
    {
        if (serialPortSelector is null)
            throw new ArgumentNullException(nameof(serialPortSelector));
        
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        
        if (loggerFactory is null)
            throw new ArgumentNullException(nameof(loggerFactory));
            
        ISerialPort serialPort;
#if ANDROID
        var androidOptions = new AndroidSerialPortOptions(options);
        serialPort = CreateSerialPort(usbSerialPort =>
        {
            var devices = usbSerialPort.Select(port => new AndroidSerialPortDevice(port)).ToImmutableList();
            var selectedDevice = devices.FirstOrDefault(i => ReferenceEquals(i, serialPortSelector(devices)));

            if (selectedDevice is null)
                throw new InvalidOperationException("Invalid serial port selection");

            return selectedDevice.GetUsbSerialPort();
        }, androidOptions, loggerFactory);
#elif DESKTOP
            var serialPortOptions = new DotNetSerialPortOptions(options);
            var serialPortLogger = loggerFactory.CreateLogger<DotNetSerialPort>();
            var logger = loggerFactory.CreateLogger(typeof(TransportFactory));
            var serialPortManager = new SerialPort.SerialPortManager();
            
            IReadOnlyCollection<NineDigit.SerialPort.ISerialPortDevice> devices;

            try
            {
                devices = await serialPortManager.GetSerialPortDevicesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException)
            {
                logger.LogWarning("Unable to get serial port devices on given platform");
                devices = [];
            }

            var serialPortDevices = devices.Select(device => new DotNetSerialPortDevice(device)).ToList();

            var selectedSerialPortDevice = serialPortSelector(serialPortDevices);
            if (selectedSerialPortDevice is null)
                throw new InvalidOperationException("No serial port selected");
            
            serialPort = new DotNetSerialPort(selectedSerialPortDevice.PortName, serialPortOptions, serialPortLogger);
#else
            throw new PlatformNotSupportedException();
#endif
        var transportLogger = loggerFactory.CreateLogger<TransportConnection>();
        var transport = new TransportConnection(serialPort, transportLogger);
        
        return transport;
    }
        
#if ANDROID
    public static AndroidSerialPort CreateSerialPort(
        UsbSerialPortSelectorDelegate serialPortSelector,
        AndroidSerialPortOptions options,
        ILoggerFactory loggerFactory)
    {
        var usbManager = GetUsbManager();
        var probeTable = ProbeTable.Default;
        var serialPort = CreateSerialPort(usbManager, probeTable, serialPortSelector, options, loggerFactory);

        return serialPort;
    }

    public static AndroidSerialPort CreateSerialPort(
        UsbManager usbManager,
        ProbeTable probeTable,
        UsbSerialPortSelectorDelegate serialPortSelector,
        AndroidSerialPortOptions options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(usbManager);
        ArgumentNullException.ThrowIfNull(serialPortSelector);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
            
        var usbSerialProber = new UsbSerialProber(probeTable);
        var ports = usbSerialProber.FindAllDrivers(usbManager).SelectMany(i => i.Ports).ToImmutableList();

        if (ports.Count == 0)
            throw new InvalidOperationException("No connected USB device was found.");

        var port = serialPortSelector(ports);
        if (port is null)
            throw new InvalidOperationException("No matching device was found.");
            
        var serialPortLogger = loggerFactory.CreateLogger<AndroidSerialPort>();
        var serialPort = new AndroidSerialPort(usbManager, port, options, serialPortLogger);
            
        return serialPort;
    }
        
    private static UsbManager GetUsbManager()
        => (UsbManager)Application.Context.GetSystemService(Context.UsbService)!;
#endif
}