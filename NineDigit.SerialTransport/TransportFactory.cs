#if ANDROID
using System.Collections.Immutable;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial;
#endif
using Microsoft.Extensions.Logging;

namespace NineDigit.SerialTransport;

/// <summary>
/// Helper for instantiating transport instance.
/// </summary>
public static class TransportFactory
{
#if DESKTOP
    public static async Task<ITransport> CreateSerialTransport(SerialPortDeviceSelectorDelegate serialPortSelector,
        SerialPortOptions options, ILoggerFactory loggerFactory, CancellationToken cancellationToken = default)
    {
        if (serialPortSelector is null)
            throw new ArgumentNullException(nameof(serialPortSelector));

        if (options is null)
            throw new ArgumentNullException(nameof(options));

        if (loggerFactory is null)
            throw new ArgumentNullException(nameof(loggerFactory));

        var serialPortLogger = loggerFactory.CreateLogger<SerialPort>();
        var logger = loggerFactory.CreateLogger(typeof(TransportFactory));
        var serialPortManager = new NineDigit.SerialPort.SerialPortManager();

        IReadOnlyCollection<NineDigit.SerialPort.ISerialPortDevice> devices;

        try
        {
            devices = await serialPortManager.GetSerialPortDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException)
        {
            logger.LogWarning("Unable to get serial port devices on given platform");
            devices = [];
        }

        var serialPortDevices = devices.Select(device => new SerialPortDevice(device)).ToList();

        var selectedSerialPortDevice = serialPortSelector(serialPortDevices);
        if (selectedSerialPortDevice is null)
            throw new InvalidOperationException("No serial port selected");

        var serialPort = new SerialPort(selectedSerialPortDevice.PortName, options, serialPortLogger);
        var transportLogger = loggerFactory.CreateLogger<TransportConnection>();
        var transport = new TransportConnection(serialPort, transportLogger);

        return transport;
    }
#elif ANDROID
    public static ITransport CreateSerialTransport(
        UsbManager usbManager,
        // IUsbPermissionService usbPermissionService,
        ProbeTable probeTable,
        UsbSerialPortSelectorDelegate serialPortSelector,
        SerialPortOptions options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(usbManager);
        ArgumentNullException.ThrowIfNull(serialPortSelector);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        
        var usbSerialProber = new UsbSerialProber(probeTable);
        var ports = usbSerialProber.FindAllDrivers(usbManager).SelectMany(i => i.Ports).ToImmutableList();

        if (ports.Count == 0)
            throw new InvalidOperationException("No compatible USB device was found.");
        
        var port = ports.FirstOrDefault(i => ReferenceEquals(i, serialPortSelector(ports)));
        if (port is null)
            throw new InvalidOperationException("No matching device was found.");
        
        var serialPortLogger = loggerFactory.CreateLogger<SerialPort>();
        var transportLogger = loggerFactory.CreateLogger<TransportConnection>();
        var serialPort = new SerialPort(port, usbManager, options, serialPortLogger);
        var transport = new TransportConnection(serialPort, transportLogger);

        return transport;
    }
#endif
}