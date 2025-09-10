#if ANDROID
using System.Collections.Immutable;
using Android.Hardware.Usb;

namespace Hoho.Android.UsbSerial.Drivers;

public interface IUsbSerialDriver
{
    UsbDevice Device { get; }
    IImmutableList<UsbSerialPort> Ports { get; }
}
#endif