namespace NineDigit.SerialTransport;

public delegate ISerialPortDevice? SerialPortDeviceSelectorDelegate(IEnumerable<ISerialPortDevice> devices);