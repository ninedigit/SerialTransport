namespace NineDigit.SerialTransport;

public interface ISerialPortDevice
{
    public int VendorId { get; }
    public string? VendorName { get; }
    public int ProductId { get; }
    public string? ProductName { get; }
    
    public string PortName { get; }
}