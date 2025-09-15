#if ANDROID
/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System.Collections.Immutable;
using Android.Hardware.Usb;
// ReSharper disable CheckNamespace

namespace Hoho.Android.UsbSerial.Drivers;

public abstract class UsbSerialDriverBase : IUsbSerialDriver
{
    private IImmutableList<UsbSerialPort>? _ports;
    
    public abstract UsbDevice Device { get; }
    public abstract UsbSerialPort Port { get; }

    public virtual IImmutableList<UsbSerialPort> Ports
        => _ports ??= ImmutableList.Create(Port);
}
#endif