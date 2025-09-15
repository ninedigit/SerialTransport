// ReSharper disable UnusedType.Global
// ReSharper disable ConvertToPrimaryConstructor
namespace NineDigit.SerialTransport;

public sealed class DefaultSerialPortDeviceSelector : ISerialPortDeviceSelector
{
    private readonly SerialPortDeviceSelectorDelegate _handler;
    
    public DefaultSerialPortDeviceSelector(SerialPortDeviceSelectorDelegate handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public ISerialPortDevice? SelectDevice(IEnumerable<ISerialPortDevice> devices)
        => _handler(devices);
}