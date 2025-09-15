#if ANDROID
using Android.Hardware.Usb;
using Android.Util;
using Java.Lang;
using Exception = Java.Lang.Exception;
using Thread = System.Threading.Thread;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Local

namespace Hoho.Android.UsbSerial.Drivers;

public class ProlificSerialDriver : UsbSerialDriverBase
{
    private const string Tag = nameof(ProlificSerialDriver);

    public ProlificSerialDriver(UsbDevice device)
    {
        Device = device;
        Port = new ProlificSerialPort(device, 0, this);
    }

    public override UsbDevice Device { get; }
    public override UsbSerialPort Port { get; }

    public static ImmutableDeviceList GetSupportedDevices()
    {
        return new ImmutableDeviceList(new Dictionary<int, int[]>
        {
            {
                UsbId.VENDOR_PROLIFIC, [
                    UsbId.PROLIFIC_PL2303,
                    UsbId.PROLIFIC_PL2303GC,
                    UsbId.PROLIFIC_PL2303GB,
                    UsbId.PROLIFIC_PL2303GT,
                    UsbId.PROLIFIC_PL2303GL,
                    UsbId.PROLIFIC_PL2303GE,
                    UsbId.PROLIFIC_PL2303GS

                ]
            }
        });
    }

    class ProlificSerialPort : CommonUsbSerialPort
    {
        protected enum DeviceType { DeviceType01, DeviceTypeT, DeviceTypeHx, DeviceTypeHxn }

        private const int UsbReadTimeoutMilliseconds = 1000;
        private const int UsbWriteTimeoutMilliseconds = 5000;

        private const int UsbRecipInterface = 0x01;

        private const int VendorReadRequest = 0x01;
        private const int VendorWriteRequest = 0x01;
        private const int VendorReadHxnRequest = 0x81;
        public const int VendorWriteHxnRequest = 0x80;

        private const int VendorOutReqType = UsbSupport.UsbDirOut | UsbConstants.UsbTypeVendor;
        private const int VendorInReqType = UsbSupport.UsbDirIn | UsbConstants.UsbTypeVendor;
        private const int CtrlOutReqType = UsbSupport.UsbDirOut | UsbConstants.UsbTypeClass | UsbRecipInterface;

        private const int WriteEndpoint = 0x02;
        private const int ReadEndpoint = 0x83;
        private const int InterruptEndpoint = 0x81;

        private const int ResetHxnRequest = 0x07;
        private const int FlushRxRequest = 0x08;
        private const int FlushTxRequest = 0x09;

        private const int SetLineRequest = 0x20; // same as CDC SET_LINE_CODING
        private const int SetControlRequest = 0x22; // same as CDC SET_CONTROL_LINE_STATE
        private const int SendBreakRequest = 0x23; // same as CDC SEND_BREAK
        private const int GetControlHxnRequest = 0x80;
        private const int GetControlRequest = 0x87;
        private const int StatusNotification = 0xa1; // similar to CDC SERIAL_STATE but different length

        /* RESET_HXN_REQUEST */
        private const int ResetHxnRxPipe = 1;
        private const int ResetHxnTxPipe = 2;

        /* SET_CONTROL_REQUEST */
        private const int ControlDtr = 0x01;
        private const int ControlRts = 0x02;

        /* GET_CONTROL_REQUEST */
        private const int GetControlFlagCd = 0x02;
        private const int GetControlFlagDsr = 0x04;
        private const int GetControlFlagRi = 0x01;
        private const int GetControlFlagCts = 0x08;

        /* GET_CONTROL_HXN_REQUEST */
        private const int GetControlHxnFlagCd = 0x40;
        private const int GetControlHxnFlagDsr = 0x20;
        private const int GetControlHxnFlagRi = 0x80;
        private const int GetControlHxnFlagCts = 0x08;

        /* interrupt endpoint read */
        private const int StatusFlagCd = 0x01;
        private const int StatusFlagDsr = 0x02;
        private const int StatusFlagRi = 0x08;
        private const int StatusFlagCts = 0x80;

        private const int StatusBufferSize = 10;
        private const int StatusByteIdx = 8;
            
        private DeviceType _deviceType = DeviceType.DeviceTypeHx;

        private UsbEndpoint? _readEndpoint;
        private UsbEndpoint? _writeEndpoint;
        private UsbEndpoint? _interruptEndpoint;

        private int _controlLinesValue = 0;

        private int? _baudRate, _dataBits;
        private StopBits? _stopBits;
        private Parity? _parity;

        private int _status;
        private volatile Thread? _readStatusThread;
        private readonly object _readStatusThreadLock = new();
        private bool _stopReadStatusThread;
        private IOException? _readStatusException;

        public ProlificSerialPort(UsbDevice device, int portNumber, ProlificSerialDriver driver)
            : base(device, portNumber)
        {
            Driver = driver;
        }

        public override IUsbSerialDriver Driver { get; }

        private byte[] InControlTransfer(int requestType, int request, int value, int index, int length)
        {
            var buffer = new byte[length];
            var connection = EnsureConnection();
            var result = connection.ControlTransfer((UsbAddressing)requestType, request, value,
                index, buffer, length, UsbReadTimeoutMilliseconds);
                
            if (result != length)
                throw new IOException($"ControlTransfer with value {value} failed: {result}");
                
            return buffer;
        }

        private void OutControlTransfer(int requestType, int request, int value, int index, byte[]? data)
        {
            var length = data?.Length ?? 0;
            var connection = EnsureConnection();
            var result = connection.ControlTransfer((UsbAddressing)requestType, request, value,
                index, data, length, UsbWriteTimeoutMilliseconds);
                
            if (result != length)
                throw new IOException($"ControlTransfer with value {value} failed: {result}");
        }

        private byte[] VendorIn(int value, int index, int length)
        {
            var request = _deviceType == DeviceType.DeviceTypeHxn ? VendorReadHxnRequest : VendorReadRequest;
            return InControlTransfer(VendorInReqType, request, value, index, length);
        }

        private void VendorOut(int value, int index, byte[]? data)
        {
            var request = _deviceType == DeviceType.DeviceTypeHxn ? VendorWriteHxnRequest : VendorWriteRequest;
            OutControlTransfer(VendorOutReqType, request, value, index, data);
        }

        private void ResetDevice()
            => PurgeHwBuffers(true, true);

        private void CtrlOut(int request, int value, int index, byte[]? data)
            => OutControlTransfer(CtrlOutReqType, request, value, index, data);

        private bool TestHxStatus()
        {
            try
            {
                InControlTransfer(VendorInReqType, VendorReadRequest, 0x8080, 0, 1);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void DoBlackMagic()
        {
            if (_deviceType == DeviceType.DeviceTypeHxn)
                return;

            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 0, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 1, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorOut(0, 1, null);
            VendorOut(1, 0, null);
            VendorOut(2, _deviceType == DeviceType.DeviceTypeHx ? 0x44 : 0x24, null);
        }

        private void SetControlLines(int newControlLinesValue)
        {
            CtrlOut(SetControlRequest, newControlLinesValue, 0, null);
            _controlLinesValue = newControlLinesValue;
        }

        private void ReadStatusThreadFunction()
        {
            try
            {
                var connection = EnsureConnection();
                
                while (!_stopReadStatusThread)
                {
                    var buffer = new byte[StatusBufferSize];
                    var readBytesCount = connection.BulkTransfer(_interruptEndpoint, buffer, StatusBufferSize, 500);
                    if (readBytesCount > 0)
                    {
                        if (readBytesCount == StatusBufferSize)
                        {
                            _status = buffer[StatusByteIdx] & 0xff;
                        }
                        else
                        {
                            throw new IOException(
                                $"Invalid CTS / DSR / CD / RI status buffer received, expected {StatusBufferSize} bytes, but received {readBytesCount}");
                        }
                    }
                }
            }
            catch (IOException e)
            {
                _readStatusException = e;
            }
        }

        private int GetStatus()
        {
            if (_readStatusThread == null && _readStatusException == null)
            {
                lock (_readStatusThreadLock)
                {
                    if (_readStatusThread == null)
                    {
                        _status = 0;
                        if (_deviceType == DeviceType.DeviceTypeHxn)
                        {
                            byte[] data = VendorIn(GetControlHxnRequest, 0, 1);
                            if ((data[0] & GetControlHxnFlagCts) == 0) _status |= StatusFlagCts;
                            if ((data[0] & GetControlHxnFlagDsr) == 0) _status |= StatusFlagDsr;
                            if ((data[0] & GetControlHxnFlagCd) == 0) _status |= StatusFlagCd;
                            if ((data[0] & GetControlHxnFlagRi) == 0) _status |= StatusFlagRi;
                        }
                        else
                        {
                            byte[] data = VendorIn(GetControlRequest, 0, 1);
                            if ((data[0] & GetControlFlagCts) == 0) _status |= StatusFlagCts;
                            if ((data[0] & GetControlFlagDsr) == 0) _status |= StatusFlagDsr;
                            if ((data[0] & GetControlFlagCd) == 0) _status |= StatusFlagCd;
                            if ((data[0] & GetControlFlagRi) == 0) _status |= StatusFlagRi;
                        }
                        var mReadStatusThreadDelegate = new ThreadStart(ReadStatusThreadFunction);

                        _readStatusThread = new Thread(mReadStatusThreadDelegate);

                        _readStatusThread.Start();

                        //mReadStatusThread = new Thread(new Runnable()
                        //{
                        //    public void run()
                        //    {
                        //        ReadStatusThreadFunction();
                        //    }
                        //});
                        //mReadStatusThread.Daemon = true;//  setDaemon(true);
                        //mReadStatusThread.Start();
                    }
                }
            }


            /* throw and clear an exception which occured in the status read thread */
            var readStatusException = _readStatusException;
            if (readStatusException != null)
            {
                _readStatusException = null;
                throw readStatusException;
            }

            return _status;
        }

        private bool TestStatusFlag(int flag)
        {
            return (GetStatus() & flag) == flag;
        }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
            {
                throw new IOException("Already open");
            }

            var usbInterface = Device.GetInterface(0);

            if (!connection.ClaimInterface(usbInterface, true))
            {
                throw new IOException("Error claiming Prolific interface 0");
            }
                
            Connection = connection;
            var opened = false;
                
            try
            {
                for (var i = 0; i < usbInterface.EndpointCount; ++i)
                {
                    var currentEndpoint = usbInterface.GetEndpoint(i);
                    switch (currentEndpoint.Address)
                    {
                        case (UsbAddressing)ReadEndpoint:
                            _readEndpoint = currentEndpoint;
                            break;

                        case (UsbAddressing)WriteEndpoint:
                            _writeEndpoint = currentEndpoint;
                            break;

                        case (UsbAddressing)InterruptEndpoint:
                            _interruptEndpoint = currentEndpoint;
                            break;
                    }
                }

                var rawDescriptors = connection.GetRawDescriptors();
                if (rawDescriptors == null || rawDescriptors.Length < 14)
                {
                    throw new IOException("Could not get device descriptors");
                }
                int usbVersion = (rawDescriptors[3] << 8) + rawDescriptors[2];
                int deviceVersion = (rawDescriptors[13] << 8) + rawDescriptors[12];
                byte maxPacketSize0 = rawDescriptors[7];

                if (Device.DeviceClass == UsbClass.Comm || maxPacketSize0 != 64)
                {
                    _deviceType = DeviceType.DeviceType01;
                }
                else if (usbVersion == 0x200)
                {
                    if (deviceVersion is 0x300 or 0x500 && TestHxStatus())
                    {
                        _deviceType = DeviceType.DeviceTypeT; // TA & TB
                    }

                    else
                    {
                        _deviceType = DeviceType.DeviceTypeHxn;
                    }
                }
                else
                {
                    _deviceType = DeviceType.DeviceTypeHx;
                }

                SetControlLines(_controlLinesValue);
                ResetDevice();

                DoBlackMagic();
                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    Connection = null;
                    connection.ReleaseInterface(usbInterface);
                }
            }
        }

        public override void Close()
        {
            if (Connection == null)
            {
                throw new IOException("Already closed");
            }
            try
            {
                _stopReadStatusThread = true;
                lock (_readStatusThreadLock)
                {
                    if (_readStatusThread != null)
                    {
                        try
                        {
                            _readStatusThread.Join();
                        }
                        catch (Exception e)
                        {
                            Log.Warn(Tag, "An error occured while waiting for status read thread", e);
                        }
                    }
                }
                ResetDevice();
            }
            finally
            {
                try
                {
                    Connection.ReleaseInterface(Device.GetInterface(0));
                }
                finally
                {
                    Connection = null;
                }
            }
        }

        public override int Read(byte[] dest, int timeoutMillis)
        {
            lock (ReadBufferLock)
            {
                var readAmt = System.Math.Min(dest.Length, ReadBuffer.Length);
                var connection = EnsureConnection();
                var numBytesRead = connection.BulkTransfer(_readEndpoint, ReadBuffer,
                    readAmt, timeoutMillis);
                
                if (numBytesRead < 0)
                    return 0;
                
                Buffer.BlockCopy(ReadBuffer, 0, dest, 0, numBytesRead);
                return numBytesRead;
            }
        }

        public override int Write(byte[] src, int timeoutMillis)
        {
            var offset = 0;
            var connection = EnsureConnection();
            
            while (offset < src.Length)
            {
                int writeLength;
                int amtWritten;

                lock (WriteBufferLock)
                {
                    byte[] writeBuffer;

                    writeLength = System.Math.Min(src.Length - offset, WriteBuffer.Length);
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

                    amtWritten = connection.BulkTransfer(_writeEndpoint,
                        writeBuffer, writeLength, timeoutMillis);
                }

                if (amtWritten <= 0)
                {
                    throw new IOException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                offset += amtWritten;
            }
            return offset;
        }

        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            if (_baudRate == baudRate && _dataBits == dataBits && _stopBits == stopBits && _parity == parity)
            {
                // Make sure no action is performed if there is nothing to change
                return;
            }

            var lineRequestData = new byte[7];

            lineRequestData[0] = (byte)(baudRate & 0xff);
            lineRequestData[1] = (byte)((baudRate >> 8) & 0xff);
            lineRequestData[2] = (byte)((baudRate >> 16) & 0xff);
            lineRequestData[3] = (byte)((baudRate >> 24) & 0xff);

            lineRequestData[4] = stopBits switch
            {
                StopBits.One => 0,
                StopBits.OnePointFive => 1,
                StopBits.Two => 2,
                _ => throw new IllegalArgumentException("Unknown stopBits value: " + stopBits)
            };

            lineRequestData[5] = parity switch
            {
                Parity.None => 0,
                Parity.Odd => 1,
                Parity.Even => 2,
                Parity.Mark => 3,
                Parity.Space => 4,
                _ => throw new IllegalArgumentException("Unknown parity value: " + parity)
            };

            lineRequestData[6] = (byte)dataBits;

            CtrlOut(SetLineRequest, 0, 0, lineRequestData);

            ResetDevice();

            _baudRate = baudRate;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _parity = parity;
        }

        public override bool GetCd()
        {
            return TestStatusFlag(StatusFlagCd);
        }

        public override bool GetCts()
        {
            return TestStatusFlag(StatusFlagCts);
        }
            
        public override bool GetDsr()
        {
            return TestStatusFlag(StatusFlagDsr);
        }

        public override bool GetDtr()
        {
            return (_controlLinesValue & ControlDtr) == ControlDtr;
        }

        public override void SetDtr(bool value)
        {
            int newControlLinesValue;
            if (value)
            {
                newControlLinesValue = _controlLinesValue | ControlDtr;
            }
            else
            {
                newControlLinesValue = _controlLinesValue & ~ControlDtr;
            }
            SetControlLines(newControlLinesValue);
        }
            
        public override bool GetRi()
        {
            return TestStatusFlag(StatusFlagRi);
        }

        public override bool GetRts()
        {
            return (_controlLinesValue & ControlRts) == ControlRts;
        }

        public override void SetRts(bool value)
        {
            int newControlLinesValue;
            if (value)
            {
                newControlLinesValue = _controlLinesValue | ControlRts;
            }
            else
            {
                newControlLinesValue = _controlLinesValue & ~ControlRts;
            }
            SetControlLines(newControlLinesValue);
        }

        public override bool PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            if (_deviceType == DeviceType.DeviceTypeHxn)
            {
                int index = 0;
                if (purgeWriteBuffers) index |= ResetHxnRxPipe;
                if (purgeReadBuffers) index |= ResetHxnTxPipe;
                if (index != 0)
                    VendorOut(ResetHxnRequest, index, null);
            }
            else
            {
                if (purgeWriteBuffers)
                    VendorOut(FlushRxRequest, 0, null);
                if (purgeReadBuffers)
                    VendorOut(FlushTxRequest, 0, null);
            }
            return purgeReadBuffers || purgeWriteBuffers;
        }
    }
}
#endif