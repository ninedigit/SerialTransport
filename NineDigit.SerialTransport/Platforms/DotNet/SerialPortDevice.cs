namespace NineDigit.SerialTransport;

internal class SerialPortDevice : ISerialPortDevice
{
    private readonly NineDigit.SerialPort.ISerialPortDevice _device;
    
    public SerialPortDevice(NineDigit.SerialPort.ISerialPortDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }
    
    public int VendorId => _device.VendorId;
    public int ProductId => _device.ProductId;
    public string? VendorName => _device.VendorName;
    public string? ProductName => _device.ProductName;
    public string PortName => _device.SerialPortName;
    
    public NineDigit.SerialPort.ISerialPortDevice GetSerialPortDevice()
        =>  _device;
}