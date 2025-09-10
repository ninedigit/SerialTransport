#if ANDROID
/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;

namespace Hoho.Android.UsbSerial.Drivers;

public class Ch34xSerialDriver : UsbSerialDriverBase
{
    private const string Tag = nameof(Ch34xSerialDriver);

    public Ch34xSerialDriver(UsbDevice device)
    {
        Device = device;
        Port = new Ch340SerialPort(device, 0, this);
    }

    public override UsbDevice Device { get; }
    public override UsbSerialPort Port { get; }

    public class Ch340SerialPort : CommonUsbSerialPort
    {
        private const int UsbTimeoutMilliseconds = 5000;
        private const int DefaultBaudRate = 9600;

        private const int SclDtr = 0x20;
        private const int SclRts = 0x40;
        private const int LcrEnableRx = 0x80;
        private const int LcrEnableTx = 0x40;
        private const int LcrStopBits2 = 0x04;
        private const int LcrCs8 = 0x03;
        private const int LcrCs7 = 0x02;
        private const int LcrCs6 = 0x01;
        private const int LcrCs5 = 0x00;

        private const int LcrMarkSpace = 0x20;
        private const int LcrParEven = 0x10;
        private const int LcrEnablePar = 0x08;

        private bool _dtr;
        private bool _rts;

        private UsbEndpoint? _readEndpoint;
        private UsbEndpoint? _writeEndpoint;

        public Ch340SerialPort(UsbDevice device, int portNumber, Ch34xSerialDriver driver)
            : base(device, portNumber)
        {
            Driver = driver;
        }

        public override IUsbSerialDriver Driver { get; }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
            {
                throw new IOException("Already opened.");
            }

            Connection = connection;
            var opened = false;
                
            try
            {
                for (var i = 0; i < Device.InterfaceCount; i++)
                {
                    var usbInterface = Device.GetInterface(i);
                    if (Connection.ClaimInterface(usbInterface, true))
                    {
                        Log.Debug(Tag, "claimInterface " + i + " SUCCESS");
                    }
                    else
                    {
                        Log.Debug(Tag, "claimInterface " + i + " FAIL");
                    }
                }

                var dataInterface = Device.GetInterface(Device.InterfaceCount - 1);
                for (var i = 0; i < dataInterface.EndpointCount; i++)
                {
                    var ep = dataInterface.GetEndpoint(i);
                    if (ep.Type == (UsbAddressing)UsbSupport.UsbEndpointXferBulk)
                    {
                        if (ep.Direction == (UsbAddressing)UsbSupport.UsbDirIn)
                        {
                            _readEndpoint = ep;
                        }
                        else
                        {
                            _writeEndpoint = ep;
                        }
                    }
                }

                Initialize();
                SetBaudRate(DefaultBaudRate);

                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    try
                    {
                        Close();
                    }
                    catch (IOException e)
                    {
                        // Ignore IOExceptions during close()
                    }
                }
            }
        }

        public override void Close()
        {
            if (Connection is null)
            {
                throw new IOException("Already closed");
            }

            // TODO: nothing sent on close, maybe needed?

            try
            {
                Connection.Close();
            }
            finally
            {
                Connection = null;
            }
        }

        public override int Read(byte[] dest, int timeoutMillis)
        {
            int numBytesRead;
            lock (ReadBufferLock)
            {
                var readAmt = Math.Min(dest.Length, ReadBuffer.Length);
                numBytesRead = Connection.BulkTransfer(_readEndpoint, ReadBuffer, readAmt,
                    timeoutMillis);
                if (numBytesRead < 0)
                {
                    // This sucks: we get -1 on timeout, not 0 as preferred.
                    // We *should* use UsbRequest, except it has a bug/api oversight
                    // where there is no way to determine the number of bytes read
                    // in response :\ -- http://b.android.com/28023
                    return 0;
                }
                Buffer.BlockCopy(ReadBuffer, 0, dest, 0, numBytesRead);
            }
            return numBytesRead;
        }

        public override int Write(byte[] src, int timeoutMillis)
        {
            var offset = 0;

            while (offset < src.Length)
            {
                int writeLength;
                int amtWritten;

                lock (WriteBufferLock)
                {
                    byte[] writeBuffer;

                    writeLength = Math.Min(src.Length - offset, WriteBuffer.Length);
                    if (offset == 0)
                    {
                        writeBuffer = src;
                    }
                    else
                    {
                        // bulkTransfer does not support offsets, make a copy.
                        Buffer.BlockCopy(src, offset, WriteBuffer, 0, writeLength);
                        writeBuffer = WriteBuffer;
                    }

                    amtWritten = Connection.BulkTransfer(_writeEndpoint, writeBuffer, writeLength,
                        timeoutMillis);
                }
                if (amtWritten <= 0)
                {
                    throw new IOException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                Log.Debug(Tag, $"Wrote amt={amtWritten} attempted={writeLength}");
                offset += amtWritten;
            }
            return offset;
        }

        private int ControlOut(int request, int value, int index)
        {
            const int reqTypeHostToDevice = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirOut;
            return Connection.ControlTransfer((UsbAddressing)reqTypeHostToDevice, request,
                value, index, null, 0, UsbTimeoutMilliseconds);
        }


        private int ControlIn(int request, int value, int index, byte[] buffer)
        {
            const int reqTypeHostToDevice = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirIn;
            return Connection.ControlTransfer((UsbAddressing)reqTypeHostToDevice, request,
                value, index, buffer, buffer.Length, UsbTimeoutMilliseconds);
        }

        private void CheckState(string msg, int request, int value, int[] expected)
        {
            var buffer = new byte[expected.Length];
            var ret = ControlIn(request, value, 0, buffer);

            if (ret < 0)
            {
                throw new IOException($"Failed send cmd [{msg}]");
            }

            if (ret != expected.Length)
            {
                throw new IOException($"Expected {expected.Length} bytes, but get {ret} [{msg}]");
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i] == -1)
                {
                    continue;
                }

                var current = buffer[i] & 0xff;
                if (expected[i] != current)
                {
                    throw new IOException($"Expected 0x{expected[i]:X} bytes, but get 0x{current:X} [ {msg} ]");
                }
            }
        }

        private void SetControlLines()
        {
            if (ControlOut(0xa4, ~((_dtr ? SclDtr : 0) | (_rts ? SclRts : 0)), 0) < 0)
            {
                throw new IOException("Failed to set control lines");
            }
        }

        private void Initialize()
        {
            CheckState("init #1", 0x5f, 0, [-1 /* 0x27, 0x30 */, 0x00]);

            if (ControlOut(0xa1, 0, 0) < 0)
            {
                throw new IOException("init failed! #2");
            }

            SetBaudRate(DefaultBaudRate);

            CheckState("init #4", 0x95, 0x2518, [-1 /* 0x56, c3*/, 0x00]);

            if (ControlOut(0x9a, 0x2518, 0x0050) < 0)
            {
                throw new IOException("init failed! #5");
            }

            CheckState("init #6", 0x95, 0x0706, [-1 /*0xf?*/, -1 /*0xec,0xee*/]);

            if (ControlOut(0xa1, 0x501f, 0xd90a) < 0)
            {
                throw new IOException("init failed! #7");
            }

            SetBaudRate(DefaultBaudRate);

            SetControlLines();

            CheckState("init #10", 0x95, 0x0706, [-1 /* 0x9f, 0xff*/, 0xee]);
        }

        private void SetBaudRate(int baudRate)
        {
            int[] baud =
            [
                50, 0x1680, 0x0024, 75, 0x6480, 0x0018, 100, 0x8B80, 0x0012,
                110, 0x9580, 0x00B4,  150, 0xB280, 0x000C, 300, 0xD980, 0x0006, 600, 0x6481, 0x0018,
                900, 0x9881, 0x0010, 1200, 0xB281, 0x000C, 1800, 0xCC81, 0x0008, 2400, 0xD981, 0x0006,
                3600, 0x3082, 0x0020, 4800, 0x6482, 0x0018, 9600, 0xB282, 0x000C, 14400, 0xCC82, 0x0008,
                19200, 0xD982, 0x0006, 33600, 0x4D83, 0x00D3, 38400, 0x6483, 0x0018, 56000, 0x9583, 0x0018,
                57600, 0x9883, 0x0010, 76800, 0xB283, 0x000C, 115200, 0xCC83, 0x0008, 128000, 0xD183, 0x003B,
                153600, 0xD983, 0x0006, 230400, 0xE683, 0x0004, 460800, 0xF383, 0x0002, 921600, 0xF387, 0x0000,
                1500000, 0xFC83, 0x0003, 2000000, 0xFD83, 0x0002
            ];

            for (var i = 0; i < baud.Length / 3; i++)
            {
                if (baud[i * 3] == baudRate)
                {
                    var ret = ControlOut(0x9a, 0x1312, baud[i * 3 + 1]);
                    if (ret < 0)
                    {
                        throw new IOException("Error setting baud rate. #1");
                    }
                    ret = ControlOut(0x9a, 0x0f2c, baud[i * 3 + 2]);
                    if (ret < 0)
                    {
                        throw new IOException("Error setting baud rate. #1");
                    }

                    return;
                }
            }


            throw new IOException("Baud rate " + baudRate + " currently not supported");
        }

        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);

            var lcr = LcrEnableRx | LcrEnableTx;

            lcr |= dataBits switch
            {
                DataBits5 => LcrCs5,
                DataBits6 => LcrCs6,
                DataBits7 => LcrCs7,
                DataBits8 => LcrCs8,
                _ => throw new Java.Lang.IllegalArgumentException("Invalid data bits: " + dataBits),
            };


            lcr |= (int)parity switch
            {
                ParityNone => lcr,
                ParityOdd => LcrEnablePar,
                ParityEven => LcrEnablePar | LcrParEven,
                ParityMark => LcrEnablePar | LcrMarkSpace,
                ParitySpace => LcrEnablePar | LcrMarkSpace | LcrParEven,
                _ => throw new Java.Lang.IllegalArgumentException("Invalid parity: " + parity),
            };

            lcr |= (int)stopBits switch
            {
                StopBitsOne => lcr,
                StopBitsOneAndHalf => throw new Java.Lang.UnsupportedOperationException("Unsupported stop bits: 1.5"),
                StopBitsTwo => LcrStopBits2,
                _ => throw new Java.Lang.IllegalArgumentException("Invalid stop bits: " + stopBits)
            };

            var ret = ControlOut(0x9a, 0x2518, lcr);
            if (ret < 0)
            {
                throw new IOException("Error setting control byte");
            }
        }

        public override bool GetCd()
        {
            return false;
        }

        public override bool GetCts()
        {
            return false;
        }

        public override bool GetDsr()
        {
            return false;
        }

        public override bool GetDtr()
        {
            return _dtr;
        }

        public override void SetDtr(bool value)
        {
            _dtr = value;
            SetControlLines();
        }

        public override bool GetRi()
        {
            return false;
        }

        public override bool GetRts()
        {
            return _rts;
        }

        public override void SetRts(bool value)
        {
            _rts = value;
            SetControlLines();
        }

        /*public EnumSet<ControlLine> getControlLines()
        {

            int status = getStatus();
            EnumSet<ControlLine> set = EnumSet.noneOf(ControlLine.class);
            if(rts) set.add(ControlLine.RTS);
            if((status & GCL_CTS) == 0) set.add(ControlLine.CTS);
            if(dtr) set.add(ControlLine.DTR);
            if((status & GCL_DSR) == 0) set.add(ControlLine.DSR);
            if((status & GCL_CD) == 0) set.add(ControlLine.CD);
            if((status & GCL_RI) == 0) set.add(ControlLine.RI);
            return set;
        }*/

        public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers)
        {
            return true;
        }
    }

    public static ImmutableDeviceList GetSupportedDevices()
    {
        return new ImmutableDeviceList(new Dictionary<int, int[]>
        {
            {
                UsbId.VENDOR_QINHENG, [
                    UsbId.QINHENG_HL340
                ]
            }
        });
    }
}
#endif