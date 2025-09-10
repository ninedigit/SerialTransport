#if ANDROID
/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;

namespace Hoho.Android.UsbSerial.Drivers;

public abstract class UsbSerialPort
{
    /** 5 data bits. */
    public const int DataBits5 = 5;

    /** 6 data bits. */
    public const int DataBits6 = 6;

    /** 7 data bits. */
    public const int DataBits7 = 7;

    /** 8 data bits. */
    public const int DataBits8 = 8;

    /** No flow control. */
    public const int FlowControlNone = 0;

    /** RTS/CTS input flow control. */
    public const int FlowControlRtsCtsIn = 1;

    /** RTS/CTS output flow control. */
    public const int FlowControlRtsCtsOut = 2;

    /** XON/XOFF input flow control. */
    public const int FlowControlXOnXOffIn = 4;

    /** XON/XOFF output flow control. */
    public const int FlowControlXOnXFffOut = 8;

    /** No parity. */
    public const int ParityNone = 0;

    /** Odd parity. */
    public const int ParityOdd = 1;

    /** Even parity. */
    public const int ParityEven = 2;

    /** Mark parity. */
    public const int ParityMark = 3;

    /** Space parity. */
    public const int ParitySpace = 4;

    /** 1 stop bit. */
    public const int StopBitsOne = 1;

    /** 1.5 stop bits. */
    public const int StopBitsOneAndHalf = 3;

    /** 2 stop bits. */
    public const int StopBitsTwo = 2;

    public abstract IUsbSerialDriver Driver { get; }
        
    /**
     * Port number within driver.
     */
    public abstract int PortNumber { get; }

    /**
     * The serial number of the underlying UsbDeviceConnection, or {@code null}.
     */
    public abstract string Serial { get; }

    /**
     * Opens and initializes the port. Upon success, caller must ensure that
     * {@link #close()} is eventually called.
     *
     * @param connection an open device connection, acquired with
     *            {@link UsbManager#openDevice(android.hardware.usb.UsbDevice)}
     * @throws IOException on error opening or initializing the port.
     */
    public abstract void Open(UsbDeviceConnection connection);

    /**
     * Closes the port.
     *
     * @throws IOException on error closing the port.
     */
    public abstract void Close();

    /**
     * Reads as many bytes as possible into the destination buffer.
     *
     * @param dest the destination byte buffer
     * @param timeoutMillis the timeout for reading
     * @return the actual number of bytes read
     * @throws IOException if an error occurred during reading
     */
    public abstract int Read(byte[] dest, int timeoutMillis);

    /**
     * Writes as many bytes as possible from the source buffer.
     *
     * @param src the source byte buffer
     * @param timeoutMillis the timeout for writing
     * @return the actual number of bytes written
     * @throws IOException if an error occurred during writing
     */
    public abstract int Write(byte[] src, int timeoutMillis);

    /**
     * Sets various serial port parameters.
     *
     * @param baudRate baud rate as an integer, for example {@code 115200}.
     * @param dataBits one of {@link #DATABITS_5}, {@link #DATABITS_6},
     *            {@link #DATABITS_7}, or {@link #DATABITS_8}.
     * @param stopBits one of {@link #STOPBITS_1}, {@link #STOPBITS_1_5}, or
     *            {@link #STOPBITS_2}.
     * @param parity one of {@link #PARITY_NONE}, {@link #PARITY_ODD},
     *            {@link #PARITY_EVEN}, {@link #PARITY_MARK}, or
     *            {@link #PARITY_SPACE}.
     * @throws IOException on error setting the port parameters
     */
    public abstract void SetParameters(
        int baudRate, int dataBits, StopBits stopBits, Parity parity);

    /**
     * Gets the CD (Carrier Detect) bit from the underlying UART.
     *
     * @return the current state, or {@code false} if not supported.
     * @throws IOException if an error occurred during reading
     */
    public abstract bool GetCd();

    /**
     * Gets the CTS (Clear To Send) bit from the underlying UART.
     *
     * @return the current state, or {@code false} if not supported.
     * @throws IOException if an error occurred during reading
     */
    public abstract bool GetCts();

    /**
     * Gets the DSR (Data Set Ready) bit from the underlying UART.
     *
     * @return the current state, or {@code false} if not supported.
     * @throws IOException if an error occurred during reading
     */
    public abstract bool GetDsr();

    /**
     * Gets the DTR (Data Terminal Ready) bit from the underlying UART.
     *
     * @return the current state, or {@code false} if not supported.
     * @throws IOException if an error occurred during reading
     */
    public abstract bool GetDtr();

    /**
     * Sets the DTR (Data Terminal Ready) bit on the underlying UART, if
     * supported.
     *
     * @param value the value to set
     * @throws IOException if an error occurred during writing
     */
    public abstract void SetDtr(bool value);

    /**
     * Gets the RI (Ring Indicator) bit from the underlying UART.
     *
     * @return the current state, or {@code false} if not supported.
     * @throws IOException if an error occurred during reading
     */
    public abstract bool GetRi();

    /**
     * Gets the RTS (Request To Send) bit from the underlying UART.
     *
     * @return the current state, or {@code false} if not supported.
     * @throws IOException if an error occurred during reading
     */
    public abstract bool GetRts();

    /**
     * Sets the RTS (Request To Send) bit on the underlying UART, if
     * supported.
     *
     * @param value the value to set
     * @throws IOException if an error occurred during writing
     */
    public abstract void SetRts(bool value);

    /**
     * Flush non-transmitted output data and / or non-read input data
     * @param flushRX {@code true} to flush non-transmitted output data
     * @param flushTX {@code true} to flush non-read input data
     * @return {@code true} if the operation was successful, or
     * {@code false} if the operation is not supported by the driver or device
     * @throws IOException if an error occurred during flush
     */
    public abstract bool PurgeHwBuffers(bool flushRx, bool flushTx);
}
#endif