#if ANDROID
using Hoho.Android.UsbSerial.Drivers;

namespace NineDigit.SerialTransport;

internal class AndroidSerialPortDevice : ISerialPortDevice
{
    private readonly UsbSerialPort _usbSerialPort;
    
    public AndroidSerialPortDevice(UsbSerialPort port)
    {
        _usbSerialPort = port ?? throw new ArgumentNullException(nameof(port));
    }

    public int VendorId => _usbSerialPort.Driver.Device.VendorId;
    public string? VendorName =>  _usbSerialPort.Driver.Device.ManufacturerName;
    public int ProductId => _usbSerialPort.Driver.Device.ProductId;
    public string? ProductName => _usbSerialPort.Driver.Device.ProductName;
    public string PortName => _usbSerialPort.Driver.Device.DeviceName;

    public UsbSerialPort GetUsbSerialPort()
        => _usbSerialPort;
}
#endif