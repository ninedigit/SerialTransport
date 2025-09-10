#if ANDROID
/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;

namespace Hoho.Android.UsbSerial.Drivers;

public abstract class CommonUsbSerialPort : UsbSerialPort
{
    public const int DefaultReadBufferSize = 16 * 1024;
    public const int DefaultWriteBufferSize = 16 * 1024;

    // non-null when open()
    protected UsbDeviceConnection? Connection = null;

    // check if connection is still available
    public bool HasConnection => Connection != null;

    protected readonly object ReadBufferLock = new();
    protected readonly object WriteBufferLock = new();

    /** Internal read buffer.  Guarded by {@link #mReadBufferLock}. */
    protected byte[] ReadBuffer;

    /** Internal write buffer.  Guarded by {@link #mWriteBufferLock}. */
    protected byte[] WriteBuffer;

    public CommonUsbSerialPort(UsbDevice device, int portNumber)
    {
        Device = device;
        PortNumber = portNumber;

        ReadBuffer = new byte[DefaultReadBufferSize];
        WriteBuffer = new byte[DefaultWriteBufferSize];
    }
    public override string ToString()
        => $"<{GetType().Name} device_name={Device.DeviceName} device_id={Device.DeviceId} port_number={PortNumber}>";

    /**
        * Returns the currently-bound USB device.
        *
        * @return the device
        */
    public UsbDevice Device { get; }

    public override int PortNumber { get; }

    /**
     * Returns the device serial number
     *  @return serial number
     */
    public override string Serial => Connection?.Serial;

    /**
     * Sets the size of the internal buffer used to exchange data with the USB
     * stack for read operations.  Most users should not need to change this.
     *
     * @param bufferSize the size in bytes
     */
    public void SetReadBufferSize(int bufferSize)
    {
        lock(ReadBufferLock) {
            if (bufferSize == ReadBuffer.Length)
            {
                return;
            }
            ReadBuffer = new byte[bufferSize];
        }
    }

    /**
     * Sets the size of the internal buffer used to exchange data with the USB
     * stack for write operations.  Most users should not need to change this.
     *
     * @param bufferSize the size in bytes
     */
    public void SetWriteBufferSize(int bufferSize)
    {
        lock(WriteBufferLock) {
            if (bufferSize == WriteBuffer.Length)
            {
                return;
            }
            WriteBuffer = new byte[bufferSize];
        }
    }

    public abstract override void Open(UsbDeviceConnection connection);

    public abstract override void Close();

    public abstract override int Read(byte[] dest, int timeoutMillis);

    public abstract override int Write(byte[] src, int timeoutMillis);

    public abstract override void SetParameters(
        int baudRate, int dataBits, StopBits stopBits, Parity parity);

    public abstract override bool GetCd();

    public abstract override bool GetCts();

    public abstract override bool GetDsr();

    public abstract override bool GetDtr();

    public abstract override void SetDtr(bool value);

    public abstract override bool GetRi();

    public abstract override bool GetRts();

    public abstract override void SetRts(bool value);

    public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers)
        => !flushReadBuffers && !flushWriteBuffers;
}
#endif