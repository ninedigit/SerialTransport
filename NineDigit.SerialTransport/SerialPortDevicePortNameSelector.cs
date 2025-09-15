// ReSharper disable UnusedType.Global
// ReSharper disable MemberCanBePrivate.Global
namespace NineDigit.SerialTransport;

public sealed class SerialPortDevicePortNameSelector : ISerialPortDeviceSelector
{
    public SerialPortDevicePortNameSelector(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException($"'{nameof(portName)}' cannot be null or whitespace.", nameof(portName));
        
        PortName = portName;
    }
    
    public string PortName { get; }
    
    public ISerialPortDevice? SelectDevice(IEnumerable<ISerialPortDevice> devices)
        => devices.FirstOrDefault(device => device.PortName == PortName);
}