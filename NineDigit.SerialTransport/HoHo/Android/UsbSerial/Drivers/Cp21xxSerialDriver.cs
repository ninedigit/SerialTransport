#if ANDROID
using Android.Hardware.Usb;
using Android.Util;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable RedundantSwitchExpressionArms

namespace Hoho.Android.UsbSerial.Drivers;

public class Cp21xxSerialDriver : UsbSerialDriverBase
{
    private const string Tag = nameof(Cp21xxSerialDriver);

    public Cp21xxSerialDriver(UsbDevice device)
    {
        Device = device;
        Port = new Cp21xxSerialPort(device, 0, this);
    }

    public override UsbDevice Device { get; }
    public override UsbSerialPort Port { get; }

    public class Cp21xxSerialPort : CommonUsbSerialPort
    {
        private const int DefaultBaudRate = 9600;
        private const int UsbWriteTimeoutMilliseconds = 5000;

        /*
         * Configuration Request Types
         */
        private const int ReqTypeHostToDevice = 0x41;
        private const int ReqTypeDeviceToHost = 0xc1;

        /*
         * Configuration Request Codes
         */
        private const int SiLabSerIfcEnableRequestCode = 0x00;
        private const int SiLabSerSetBaudDivRequestCode = 0x01;
        private const int SiLabSerSetLineCtlRequestCode = 0x03;
        private const int SiLabSerSetMhsRequestCode = 0x07;
        private const int SiLabSerSetBaudRate = 0x1E;
        private const int SiLabSerFlushRequestCode = 0x12;

        private const int FlushReadCode = 0x0a;
        private const int FlushWriteCode = 0x05;

        private const int GetModemStatusRequest = 0x08; // 0x08 Get modem status. 
        private const int ModemStatusCts = 0x10;
        private const int ModemStatusDsr = 0x20;
        private const int ModemStatusRi = 0x40;
        private const int ModemStatusCd = 0x80;
        /*
         * SILABSER_IFC_ENABLE_REQUEST_CODE
         */
        private const int UartEnable = 0x0001;
        private const int UartDisable = 0x0000;

        /*
         * SILABSER_SET_BAUDDIV_REQUEST_CODE
         */
        private const int BaudRateGenFreq = 0x384000;

        /*
         * SILABSER_SET_MHS_REQUEST_CODE
         */
        private const int McrDtr = 0x0001;
        private const int McrRts = 0x0002;
        private const int McrAll = 0x0003;

        private const int ControlWriteDtr = 0x0100;
        private const int ControlWriteRts = 0x0200;

        private UsbEndpoint? _readEndpoint;
        private UsbEndpoint? _writeEndpoint;

        public Cp21xxSerialPort(UsbDevice device, int portNumber, Cp21xxSerialDriver driver)
            : base(device, portNumber)
        {
            Driver = driver;
        }

        public override IUsbSerialDriver Driver { get; }

        private int SetConfigSingle(int request, int value)
        {
            return EnsureConnection().ControlTransfer((UsbAddressing)ReqTypeHostToDevice, request, value,
                0, null, 0, UsbWriteTimeoutMilliseconds);
        }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
                throw new IOException("Already opened.");

            Connection = connection;
            var opened = false;
                
            try
            {
                for (var i = 0; i < Device.InterfaceCount; i++)
                {
                    var usbInterface = Device.GetInterface(i);
                    if (Connection.ClaimInterface(usbInterface, true))
                    {
                        Log.Debug(Tag, $"claimInterface {i} SUCCESS");
                    }
                    else
                    {
                        Log.Debug(Tag, $"claimInterface {i} FAIL");
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

                SetConfigSingle(SiLabSerIfcEnableRequestCode, UartEnable);
                SetConfigSingle(SiLabSerSetMhsRequestCode, McrAll | ControlWriteDtr | ControlWriteRts);
                SetConfigSingle(SiLabSerSetBaudDivRequestCode, BaudRateGenFreq / DefaultBaudRate);
                //            setParameters(DEFAULT_BAUD_RATE, DEFAULT_DATA_BITS, DEFAULT_STOP_BITS, DEFAULT_PARITY);
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
            if (Connection == null)
            {
                throw new IOException("Already closed");
            }
            try
            {
                SetConfigSingle(SiLabSerIfcEnableRequestCode, UartDisable);
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
            lock(ReadBufferLock)
            {
                var readAmt = Math.Min(dest.Length, ReadBuffer.Length);
                var connection = EnsureConnection();
                
                numBytesRead = connection.BulkTransfer(_readEndpoint, ReadBuffer, readAmt, timeoutMillis);
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

        public override int Write(byte[] src, int timeoutMilliseconds)
        {
            var offset = 0;
            var connection = EnsureConnection();

            while (offset < src.Length)
            {
                int writeLength;
                int amtWritten;
                lock(WriteBufferLock)
                {
                    writeLength = src.Length - offset;
                    amtWritten = connection.BulkTransfer(_writeEndpoint, src, offset, writeLength, timeoutMilliseconds);
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

        private void SetBaudRate(int baudRate)
        {
            var data = new[] {
                (byte) ( baudRate & 0xff),
                (byte) ((baudRate >> 8 ) & 0xff),
                (byte) ((baudRate >> 16) & 0xff),
                (byte) ((baudRate >> 24) & 0xff)
            };
            
            var connection = EnsureConnection();
            var ret = connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, SiLabSerSetBaudRate, 0, 0,
                data, 4, UsbWriteTimeoutMilliseconds);
                
            if (ret < 0)
                throw new IOException("Error setting baud rate.");
        }

        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);

            var configDataBits = 0;
                
            configDataBits |= dataBits switch
            {
                DataBits5 => 0x0500,
                DataBits6 => 0x0600,
                DataBits7 => 0x0700,
                DataBits8 => 0x0800,
                _ => 0x0800
            };

            switch (parity)
            {
                case Parity.Odd:
                    configDataBits |= 0x0010;
                    break;
                case Parity.Even:
                    configDataBits |= 0x0020;
                    break;
            }

            switch (stopBits)
            {
                case StopBits.One:
                    configDataBits |= 0;
                    break;
                case StopBits.Two:
                    configDataBits |= 2;
                    break;
            }
                
            SetConfigSingle(SiLabSerSetLineCtlRequestCode, configDataBits);
        }

        private int GetStatus()
        {
            var data = new byte[1];
            var connection = EnsureConnection();
            var result = connection.ControlTransfer((UsbAddressing)ReqTypeDeviceToHost, GetModemStatusRequest,
                0, 0, data, data.Length, UsbWriteTimeoutMilliseconds);
                
            if (result != 1)
                throw new IOException("Get modem status failed: result=" + result);
                
            return data[0];
        }

        public override bool GetCd()
        {
            return (GetStatus() & ModemStatusCd) != 0;
        }

        public override bool GetCts()
        {
            return (GetStatus() & ModemStatusCts) != 0;
        }

        public override bool GetDsr()
        {
            return (GetStatus() & ModemStatusDsr) != 0;
        }

        public override bool GetDtr()
        {
            return (GetStatus() & McrDtr) != 0;
        }

        public override void SetDtr(bool value)
        {
            SetConfigSingle(SiLabSerSetMhsRequestCode, (value ? McrDtr : 0) | ControlWriteDtr);
        }

        public override bool GetRi()
        {
            return (GetStatus() & ModemStatusRi) != 0;
        }

        public override bool GetRts()
        {
            return (GetStatus() & McrRts) != 0;
        }

        public override void SetRts(bool value)
        {
            SetConfigSingle(SiLabSerSetMhsRequestCode, (value ? McrRts : 0) | ControlWriteRts);
        }

        public override bool PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            var value = (purgeReadBuffers ? FlushReadCode : 0)
                        | (purgeWriteBuffers ? FlushWriteCode : 0);

            if (value != 0)
            {
                SetConfigSingle(SiLabSerFlushRequestCode, value);
            }

            return true;
        }
    }

    public static ImmutableDeviceList GetSupportedDevices()
    {
        return new ImmutableDeviceList(new Dictionary<int, int[]>
        {
            {
                UsbId.VENDOR_SILABS, [
                    UsbId.SILABS_CP2102,
                    UsbId.SILABS_CP2105,
                    UsbId.SILABS_CP2108,
                    UsbId.SILABS_CP2110
                ]
            }
        });
    }
}
#endif