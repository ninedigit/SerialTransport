#if ANDROID
using System.Collections.Immutable;
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial;
#endif
// ReSharper disable UnusedMember.Global

namespace NineDigit.SerialTransport;

/// <summary>
/// Helper for instantiating transport instance.
/// </summary>
public static class TransportFactory
{
#if DESKTOP
    public static ITransport CreateSerialTransport(ISerialPortDeviceSelector serialPortDeviceSelector,
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
        var transport = new TransportConnection(serialPort);

        return transport;
    }
#elif ANDROID
    public static ITransport CreateSerialTransport(
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
        var transport = new TransportConnection(serialPort);

        return transport;
    }
#endif
}