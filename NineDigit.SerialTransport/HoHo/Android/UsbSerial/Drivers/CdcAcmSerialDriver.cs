#if ANDROID
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using Hoho.Android.UsbSerial.Helpers;
using Java.Lang;
using Java.Nio;
using IOException = Java.IO.IOException;
using Math = System.Math;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMethodReturnValue.Local

namespace Hoho.Android.UsbSerial.Drivers;

public class CdcAcmSerialDriver : UsbSerialDriverBase
{
    private const string Tag = nameof(CdcAcmSerialDriver);

    // Additional constructor for ProbeDevice called by Reflection
    // Note that this defaults to enableAsyncReads = false as
    // there is no support for passing additional arguments from ProbeDevice
    public CdcAcmSerialDriver(UsbDevice device)
        : this(device, enableAsyncReads: false)
    {
    }
        
    public CdcAcmSerialDriver(UsbDevice device, bool? enableAsyncReads = null)
    {
        Device = device;
        Port = new CdcAcmSerialPort(device, 0, this, enableAsyncReads);
    }

    public override UsbDevice Device { get; }
    public override UsbSerialPort Port { get; }

    class CdcAcmSerialPort : CommonUsbSerialPort
    {
        private readonly bool _enableAsyncReads;
        private UsbInterface? _controlInterface;
        private UsbInterface? _dataInterface;

        private UsbEndpoint? _controlEndpoint;
        private UsbEndpoint? _readEndpoint;
        private UsbEndpoint? _writeEndpoint;

        private bool _mRts;
        private bool _mDtr;

        private const int UsbRecipInterface = 0x01;
        private static int UsbRtAcm = UsbConstants.UsbTypeClass | UsbRecipInterface;

        private const int SetLineCoding = 0x20; // USB CDC 1.1 section 6.2
        private const int GetLineCoding = 0x21;
        private const int SetControlLineState = 0x22;
        private const int SendBreak = 0x23;

        //public CdcAcmSerialPort(UsbDevice device, int portNumber) : base(device, portNumber)
        //{
        //    mEnableAsyncReads = (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1);
        //}

        public CdcAcmSerialPort(UsbDevice device, int portNumber, CdcAcmSerialDriver driver, bool? enableAsyncReads = null)
            : base(device, portNumber)
        {
            if (enableAsyncReads != null)
            {
                _enableAsyncReads = enableAsyncReads.Value;
            }
            else
            {
                _enableAsyncReads = Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1;
            }
            
            Driver = driver;
        }

        public override IUsbSerialDriver Driver { get; }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
                throw new IOException("Already open");

            Connection = connection;
            var opened = false;

            try
            {
                if (1 == Device.InterfaceCount)
                {
                    Log.Debug(Tag, "device might be castrated ACM device, trying single interface logic");
                    OpenSingleInterface();
                }
                else
                {
                    Log.Debug(Tag, "trying default interface logic");
                    OpenInterface();
                }

                if (_enableAsyncReads)
                {
                    Log.Debug(Tag, "Async reads enabled");
                }
                else
                {
                    Log.Debug(Tag, "Async reads disabled.");
                }


                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    Connection = null;
                    // just to be on the save side
                    _controlEndpoint = null;
                    _readEndpoint = null;
                    _writeEndpoint = null;
                }
            }
        }

        private void OpenSingleInterface()
        {
            // the following code is inspired by the cdc-acm driver
            // in the linux kernel

            _controlInterface = Device.GetInterface(0);
            Log.Debug(Tag, "Control interface: " + _controlInterface);

            _dataInterface = Device.GetInterface(0);
            Log.Debug(Tag, "Data interface: " + _dataInterface);

            var connection = EnsureConnection();
            
            if (!connection.ClaimInterface(_controlInterface, true))
                throw new IOException("Could not claim shared control/data interface.");

            var endCount = _controlInterface.EndpointCount;
            if (endCount < 3)
            {
                Log.Debug(Tag, "not enough endpoints - need 3. count=" + endCount);
                throw new IOException("Insufficient number of endpoints(" + endCount + ")");
            }

            // Analyse endpoints for their properties
            _controlEndpoint = null;
            _readEndpoint = null;
            _writeEndpoint = null;
                
            for (var i = 0; i < endCount; ++i)
            {
                var ep = _controlInterface.GetEndpoint(i);
                if (ep.Direction == UsbAddressing.In && ep.Type == UsbAddressing.XferInterrupt)
                {
                    Log.Debug(Tag, "Found controlling endpoint");
                    _controlEndpoint = ep;
                }
                else if (ep.Direction == UsbAddressing.In && ep.Type == UsbAddressing.XferBulk)
                {
                    Log.Debug(Tag, "Found reading endpoint");
                    _readEndpoint = ep;
                }
                else if (ep.Direction == UsbAddressing.Out && ep.Type == UsbAddressing.XferBulk)
                {
                    Log.Debug(Tag, "Found writing endpoint");
                    _writeEndpoint = ep;
                }

                if (_controlEndpoint != null && _readEndpoint != null && _writeEndpoint != null)
                {
                    Log.Debug(Tag, "Found all required endpoints");
                    break;
                }
            }

            if (_controlEndpoint == null || _readEndpoint == null || _writeEndpoint == null)
            {
                Log.Debug(Tag, "Could not establish all endpoints");
                throw new IOException("Could not establish all endpoints");
            }
        }

        private void OpenInterface()
        {
            Log.Debug(Tag, "claiming interfaces, count=" + Device.InterfaceCount);

            _controlInterface = Device.GetInterface(0);
                
            Log.Debug(Tag, "Control iface=" + _controlInterface);
            // class should be USB_CLASS_COMM

            var connection = EnsureConnection();
            
            if (!connection.ClaimInterface(_controlInterface, true))
                throw new IOException("Could not claim control interface.");

            _controlEndpoint = _controlInterface.GetEndpoint(0);
            Log.Debug(Tag, "Control endpoint direction: " + _controlEndpoint.Direction);

            Log.Debug(Tag, "Claiming data interface.");
            _dataInterface = Device.GetInterface(1);
            Log.Debug(Tag, "data iface=" + _dataInterface);
            // class should be USB_CLASS_CDC_DATA

            if (!connection.ClaimInterface(_dataInterface, true))
            {
                throw new IOException("Could not claim data interface.");
            }
            _readEndpoint = _dataInterface.GetEndpoint(1);
            Log.Debug(Tag, "Read endpoint direction: " + _readEndpoint.Direction);
            _writeEndpoint = _dataInterface.GetEndpoint(0);
            Log.Debug(Tag, "Write endpoint direction: " + _writeEndpoint.Direction);
        }

        private int SendAcmControlMessage(int request, int value, byte[]? buf)
            => EnsureConnection().ControlTransfer((UsbAddressing)0x21, request, value, 0, buf, buf?.Length ?? 0, 5000);

        public override void Close()
        {
            if (Connection is null)
                throw new IOException("Already closed");
            
            Connection.Close();
            Connection = null;
        }

        public override int Read(byte[] dest, int timeoutMillis)
        {
            var connection = EnsureConnection();
            
            if (_enableAsyncReads)
            {
                var request = new UsbRequest();
                
                try
                {
                    request.Initialize(connection, _readEndpoint);

                    // CJM: Xamarin bug: ByteBuffer.Wrap is supposed to be a two way update
                    // Changes made to one buffer should reflect in the other.  It's not working
                    // As a work around, I added a new method as an extension that uses JNI to turn
                    // a new byte[] array. I then used BlockCopy to copy the bytes back the original array
                    // see https://forums.xamarin.com/discussion/comment/238396/#Comment_238396
                    //
                    // Old work around:
                    // as a work around, we populate dest with a call to buf.Get()
                    // see https://bugzilla.xamarin.com/show_bug.cgi?id=20772
                    // and https://bugzilla.xamarin.com/show_bug.cgi?id=31260

                    var buf = ByteBuffer.Wrap(dest);
                    if (!request.Queue(buf, dest.Length))
                    {
                        throw new IOException("Error queueing request.");
                    }

                    var response = connection.RequestWait();
                    if (response == null)
                    {
                        throw new IOException("Null response");
                    }

                    var read = buf.Position();
                    if (read > 0)
                    {
                        // CJM: This differs from the Java implementation.  The dest buffer was
                        // not getting the data back.

                        // 1st work around, no longer used
                        //buf.Rewind();
                        //buf.Get(dest, 0, dest.Length);

                        System.Buffer.BlockCopy(buf.ToByteArray(), 0, dest, 0, dest.Length);

                        Log.Debug(Tag, HexDump.DumpHexString(dest, 0, Math.Min(32, dest.Length)));
                        return read;
                    }
                    else
                    {
                        return 0;
                    }
                }
                finally
                {
                    request.Close();
                }
            }

            int numBytesRead;
            lock (ReadBufferLock)
            {
                var readAmt = Math.Min(dest.Length, ReadBuffer.Length);
                numBytesRead = connection.BulkTransfer(_readEndpoint, ReadBuffer, readAmt,
                    timeoutMillis);
                if (numBytesRead < 0)
                {
                    // This sucks: we get -1 on timeout, not 0 as preferred.
                    // We *should* use UsbRequest, except it has a bug/api oversight
                    // where there is no way to determine the number of bytes read
                    // in response :\ -- http://b.android.com/28023
                    if (timeoutMillis == Integer.MaxValue)
                    {
                        // Hack: Special case "~infinite timeout" as an error.
                        return -1;
                    }
                    return 0;
                }
                System.Buffer.BlockCopy(ReadBuffer, 0, dest, 0, numBytesRead);
            }
            return numBytesRead;
        }

        public override int Write(byte[] src, int timeoutMillis)
        {
            // TODO(mikey): Nearly identical to FtdiSerial write. Refactor.
            var offset = 0;
            var connection = EnsureConnection();

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
                        System.Buffer.BlockCopy(src, offset, WriteBuffer, 0, writeLength);
                        writeBuffer = WriteBuffer;
                    }

                    amtWritten = connection.BulkTransfer(_writeEndpoint, writeBuffer, writeLength,
                        timeoutMillis);
                }
                if (amtWritten <= 0)
                {
                    throw new IOException("Error writing " + writeLength
                                                           + " bytes at offset " + offset + " length=" + src.Length);
                }

                Log.Debug(Tag, "Wrote amt=" + amtWritten + " attempted=" + writeLength);
                offset += amtWritten;
            }
            return offset;
        }


        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            byte stopBitsByte = stopBits switch
            {
                StopBits.One => 0,
                StopBits.OnePointFive => 1,
                StopBits.Two => 2,
                _ => throw new IllegalArgumentException("Bad value for stopBits: " + stopBits)
            };

            byte parityBitesByte = parity switch
            {
                Parity.None => 0,
                Parity.Odd => 1,
                Parity.Even => 2,
                Parity.Mark => 3,
                Parity.Space => 4,
                _ => throw new IllegalArgumentException("Bad value for parity: " + parity)
            };

            byte[] msg =
            [
                (byte) ( baudRate & 0xff),
                (byte) ((baudRate >> 8 ) & 0xff),
                (byte) ((baudRate >> 16) & 0xff),
                (byte) ((baudRate >> 24) & 0xff),
                stopBitsByte,
                parityBitesByte,
                (byte) dataBits
            ];
            SendAcmControlMessage(SetLineCoding, 0, msg);
        }

        public override bool GetCd()
        {
            return false;  // TODO
        }

        public override bool GetCts()
        {
            return false;  // TODO
        }

        public override bool GetDsr()
        {
            return false;  // TODO
        }

        public override bool GetDtr()
        {
            return _mDtr;
        }

        public override void SetDtr(bool value)
        {
            _mDtr = value;
            SetDtrRts();
        }

        public override bool GetRi()
        {
            return false;  // TODO
        }

        public override bool GetRts()
        {
            return _mRts;
        }

        public override void SetRts(bool value)
        {
            _mRts = value;
            SetDtrRts();
        }

        private void SetDtrRts()
        {
            var value = (_mRts ? 0x2 : 0) | (_mDtr ? 0x1 : 0);
            SendAcmControlMessage(SetControlLineState, value, null);
        }
    }

    public static ImmutableDeviceList GetSupportedDevices()
    {
        return new ImmutableDeviceList(new Dictionary<int, int[]>
        {
            {
                UsbId.VENDOR_ARDUINO, [
                    UsbId.ARDUINO_UNO,
                    UsbId.ARDUINO_UNO_R3,
                    UsbId.ARDUINO_MEGA_2560,
                    UsbId.ARDUINO_MEGA_2560_R3,
                    UsbId.ARDUINO_SERIAL_ADAPTER,
                    UsbId.ARDUINO_SERIAL_ADAPTER_R3,
                    UsbId.ARDUINO_MEGA_ADK,
                    UsbId.ARDUINO_MEGA_ADK_R3,
                    UsbId.ARDUINO_LEONARDO,
                    UsbId.ARDUINO_MICRO
                ]
            },
            {
                UsbId.VENDOR_VAN_OOIJEN_TECH, [
                    UsbId.VAN_OOIJEN_TECH_TEENSYDUINO_SERIAL
                ]
            },
            {
                UsbId.VENDOR_ATMEL, [
                    UsbId.ATMEL_LUFA_CDC_DEMO_APP
                ]
            },
            {
                UsbId.VENDOR_ELATEC, [
                    UsbId.ELATEC_TWN3_CDC,
                    UsbId.ELATEC_TWN4_MIFARE_NFC,
                    UsbId.ELATEC_TWN4_CDC
                ]
            },
            {
                UsbId.VENDOR_LEAFLABS, [
                    UsbId.LEAFLABS_MAPLE
                ]
            }
        });
    }
}
#endif