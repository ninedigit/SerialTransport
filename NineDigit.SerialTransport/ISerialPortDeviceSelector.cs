namespace NineDigit.SerialTransport;

public interface ISerialPortDeviceSelector
{
    public ISerialPortDevice? SelectDevice(IEnumerable<ISerialPortDevice> devices);
}