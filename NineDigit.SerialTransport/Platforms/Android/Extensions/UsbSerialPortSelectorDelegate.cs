#if ANDROID
using System.Collections.Immutable;
using Hoho.Android.UsbSerial.Drivers;

namespace NineDigit.SerialTransport;

public delegate UsbSerialPort? UsbSerialPortSelectorDelegate(IImmutableList<UsbSerialPort> serialPorts);
#endif