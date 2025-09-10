#if ANDROID
/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System.Collections.Immutable;
using Android.Hardware.Usb;
using Android.Util;
using Java.Lang;
using IOException = System.IO.IOException;
using Math = System.Math;

/*
 * driver is implemented from various information scattered over FTDI documentation
 *
 * baud rate calculation https://www.ftdichip.com/Support/Documents/AppNotes/AN232B-05_BaudRates.pdf
 * control bits https://www.ftdichip.com/Firmware/Precompiled/UM_VinculumFirmware_V205.pdf
 * device type https://www.ftdichip.com/Support/Documents/AppNotes/AN_233_Java_D2XX_for_Android_API_User_Manual.pdf -> bvdDevice
 *
 */

namespace Hoho.Android.UsbSerial.Drivers;

public class FtdiSerialDriver : UsbSerialDriverBase
{
    private const string Tag = nameof(FtdiSerialDriver);
        
    private enum DeviceType
    {
        TypeBm,
        TypeAm,
        Type2232C,
        TypeR,
        Type2232H,
        Type4232H
    }

    public FtdiSerialDriver(UsbDevice device)
    {
        Device = device;
        Port = new FtdiSerialPort(device, 0, this);
        Ports = Enumerable.Range(0, device.InterfaceCount)
            .Select(port => new FtdiSerialPort(device, port, this))
            .Cast<UsbSerialPort>()
            .ToImmutableList();
    }

    public override UsbDevice Device { get; }
    public override UsbSerialPort Port { get; }
    public override IImmutableList<UsbSerialPort> Ports { get; }

    private class FtdiSerialPort : CommonUsbSerialPort
    {
        private const int UsbWriteTimeoutMilliseconds = 5000;
        private const int ReadHeaderLength = 2; // contains MODEM_STATUS

        // https://developer.android.com/reference/android/hardware/usb/UsbConstants#USB_DIR_IN
        private const int ReqTypeHostToDevice = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirOut; // UsbConstants.USB_DIR_OUT;
        private const int ReqTypeDeviceToHost = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirIn;   // UsbConstants.USB_DIR_IN;

        private const int ResetRequest = 0;
        private const int ModemControlRequest = 1;
        private const int SetBaudRateRequest = 3;
        private const int SetDataRequest = 4;
        private const int GetModemStatusRequest = 5;
        private const int SetLatencyTimerRequest = 9;
        private const int GetLatencyTimerRequest = 10;

        private const int ModemControlDtrEnable = 0x0101;
        private const int ModemControlDtrDisable = 0x0100;
        private const int ModemControlRtsEnable = 0x0202;
        private const int ModemControlRtsDisable = 0x0200;
        private const int ModemStatusCts = 0x10;
        private const int ModemStatusDsr = 0x20;
        private const int ModemStatusRi = 0x40;
        private const int ModemStatusCd = 0x80;
        private const int ResetAll = 0;
        private const int ResetPurgeRx = 1;
        private const int ResetPurgeTx = 2;

        private bool _baudRateWithPort = false;
        private bool _dtr;
        private bool _rts;
        private int _breakConfig;

        public FtdiSerialPort(UsbDevice device, int portNumber)
            : base(device, portNumber)
        {
        }

        public FtdiSerialPort(UsbDevice device, int portNumber, FtdiSerialDriver driver)
            : base(device, portNumber)
        {
            Driver = driver;
        }

        public override IUsbSerialDriver Driver { get; }

        public void Reset()
        {
            var result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, ResetRequest,
                ResetAll, PortNumber + 1, null, 0, UsbWriteTimeoutMilliseconds);
                
            if (result != 0)
                throw new IOException("Reset failed: result=" + result);
        }

        public override void Open(UsbDeviceConnection connection)
        {
            if (Connection != null)
                throw new IOException("Already open");
                
            Connection = connection;
            var opened = false;
                
            try {
                for (var i = 0; i < Device.InterfaceCount; i++)
                {
                    if (connection.ClaimInterface(Device.GetInterface(i), true))
                    {
                        Log.Debug(Tag, "claimInterface " + i + " SUCCESS");
                    }
                    else
                    {
                        throw new IOException("Error claiming interface " + i);
                    }
                }
                Reset();
                opened = true;
            } finally {
                if (!opened)
                {
                    Close();
                    Connection = null;
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
                Connection.Close();
            }
            finally
            {
                Connection = null;
            }
        }

        public override int Read(byte[] dest, int timeoutMilliseconds)
        {
            var endpoint = Device.GetInterface(0).GetEndpoint(0);

            lock(ReadBufferLock)
            {
                var readAmt = Math.Min(dest.Length, ReadBuffer.Length);

                // todo: replace with async call
                var totalBytesRead = Connection.BulkTransfer(endpoint, ReadBuffer, readAmt, timeoutMilliseconds);

                if (totalBytesRead < ReadHeaderLength)
                    throw new IOException("Expected at least " + ReadHeaderLength + " bytes");

                return ReadFilter(dest, totalBytesRead, endpoint.MaxPacketSize);
            }
        }

        protected int ReadFilter(byte[] buffer, int totalBytesRead, int maxPacketSize)
        {
            var destPos = 0;

            for (var srcPos = 0; srcPos < totalBytesRead; srcPos += maxPacketSize)
            {
                var length = Math.Min(srcPos + maxPacketSize, totalBytesRead) - (srcPos + ReadHeaderLength);
                if (length < 0)
                    throw new IOException("Expected at least " + ReadHeaderLength + " bytes");

                Buffer.BlockCopy(ReadBuffer, srcPos + ReadHeaderLength, buffer, destPos, length);
                destPos += length;
            }
            return destPos;
        }

        public override int Write(byte[] src, int timeoutMilliseconds)
        {
            var endpoint = Device.GetInterface(0).GetEndpoint(1);
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

                    amtWritten = Connection.BulkTransfer(endpoint, writeBuffer, writeLength, timeoutMilliseconds);
                }

                if (amtWritten <= 0)
                {
                    throw new IOException("Error writing " + writeLength
                                                           + " bytes at offset " + offset + " length=" + src.Length);
                }

                Log.Debug(Tag, "Wrote amtWritten=" + amtWritten + " attempted=" + writeLength);
                offset += amtWritten;
            }
            return offset;
        }


        private int SetBaudRate(int baudRate)
        {
            int divisor, subdivisor, effectiveBaudRate;

            if (baudRate > 3500000)
            {
                throw new UnsupportedOperationException("Baud rate to high");
            }
            else if (baudRate >= 2500000)
            {
                divisor = 0;
                subdivisor = 0;
                effectiveBaudRate = 3000000;
            }
            else if (baudRate >= 1750000)
            {
                divisor = 1;
                subdivisor = 0;
                effectiveBaudRate = 2000000;
            }
            else
            {
                divisor = (24000000 << 1) / baudRate;
                divisor = (divisor + 1) >> 1; // round
                subdivisor = divisor & 0x07;
                divisor >>= 3;
                if (divisor > 0x3fff) // exceeds bit 13 at 183 baud
                    throw new UnsupportedOperationException("Baud rate to low");
                effectiveBaudRate = (24000000 << 1) / ((divisor << 3) + subdivisor);
                effectiveBaudRate = (effectiveBaudRate + 1) >> 1;
            }
            double baudRateError = Math.Abs(1.0 - (effectiveBaudRate / (double)baudRate));
            if (baudRateError >= 0.031) // can happen only > 1.5Mbaud
                throw new UnsupportedOperationException(string.Format("Baud rate deviation %.1f%% is higher than allowed 3%%", baudRateError * 100));
            int value = divisor;
            int index = 0;
            switch (subdivisor)
            {
                case 0: break; // 16,15,14 = 000 - sub-integer divisor = 0
                case 4: value |= 0x4000; break; // 16,15,14 = 001 - sub-integer divisor = 0.5
                case 2: value |= 0x8000; break; // 16,15,14 = 010 - sub-integer divisor = 0.25
                case 1: value |= 0xc000; break; // 16,15,14 = 011 - sub-integer divisor = 0.125
                case 3: value |= 0x0000; index |= 1; break; // 16,15,14 = 100 - sub-integer divisor = 0.375
                case 5: value |= 0x4000; index |= 1; break; // 16,15,14 = 101 - sub-integer divisor = 0.625
                case 6: value |= 0x8000; index |= 1; break; // 16,15,14 = 110 - sub-integer divisor = 0.75
                case 7: value |= 0xc000; index |= 1; break; // 16,15,14 = 111 - sub-integer divisor = 0.875
            }
            if (_baudRateWithPort)
            {
                index <<= 8;
                index |= PortNumber + 1;
            }
            int result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, SetBaudRateRequest,
                value, index, null, 0, UsbWriteTimeoutMilliseconds);

            if (result != 0)
            {
                throw new IOException("Setting baudrate failed: result=" + result);
            }

            return effectiveBaudRate;
        }


        public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            if (baudRate <= 0)
            {
                throw new IllegalArgumentException("Invalid baud rate: " + baudRate);
            }

            SetBaudRate(baudRate);

            int config = dataBits;

            switch (dataBits)
            {
                case DataBits5:
                case DataBits6:
                    throw new UnsupportedOperationException("Unsupported data bits: " + dataBits);
                case DataBits7:
                case DataBits8:
                    config |= dataBits;
                    break;
                default:
                    throw new IllegalArgumentException("Invalid data bits: " + dataBits);
            }

            switch (parity)
            {
                case Parity.None:
                    break;
                case Parity.Odd:
                    config |= 0x100;
                    break;
                case Parity.Even:
                    config |= 0x200;
                    break;
                case Parity.Mark:
                    config |= 0x300;
                    break;
                case Parity.Space:
                    config |= 0x400;
                    break;
                default:
                    throw new IllegalArgumentException("Unknown parity value: " + parity);
            }

            switch (stopBits)
            {
                case StopBits.One:
                    break;
                case StopBits.OnePointFive:
                    throw new UnsupportedOperationException("Unsupported stop bits: 1.5");
                case StopBits.Two:
                    config |= 0x1000;
                    break;
                default:
                    throw new IllegalArgumentException("Unknown stopBits value: " + stopBits);
            }

            var result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, SetDataRequest, config,
                PortNumber + 1, null, 0, UsbWriteTimeoutMilliseconds);

            if (result != 0)
            {
                throw new IOException("Setting parameters failed: result=" + result);
            }
            _breakConfig = config;
        }

        private int GetStatus()
        {
            var data = new byte[2];
            var result = Connection.ControlTransfer((UsbAddressing)ReqTypeDeviceToHost, GetModemStatusRequest,
                0, PortNumber + 1, data, data.Length, UsbWriteTimeoutMilliseconds);
            if (result != 2) {
                throw new IOException("Get modem status failed: result=" + result);
            }
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
            return _dtr;
        }

        public override void SetDtr(bool value)
        {
            var result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, ModemControlRequest,
                value ? ModemControlDtrEnable : ModemControlDtrDisable, PortNumber + 1, null, 0, UsbWriteTimeoutMilliseconds);
            if (result != 0)
            {
                throw new IOException("Set DTR failed: result=" + result);
            }
            _dtr = value;
        }

        public override bool GetRi()
        {
            return (GetStatus() & ModemStatusRi) != 0;
        }

        public override bool GetRts()
        {
            return _rts;
        }

        public override void SetRts(bool value)
        {
            var result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, ModemControlRequest,
                value ? ModemControlRtsEnable : ModemControlRtsDisable, PortNumber + 1, null, 0, UsbWriteTimeoutMilliseconds);
            if (result != 0)
            {
                throw new IOException("Set RTS failed: result=" + result);
            }
            _rts = value;
        }

        public override bool PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            if (purgeWriteBuffers)
            {
                var result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, ResetRequest,
                    ResetPurgeRx, PortNumber + 1, null, 0, UsbWriteTimeoutMilliseconds);
                if (result != 0)
                {
                    throw new IOException("Flushing RX failed: result=" + result);
                }
            }
            if (purgeReadBuffers)
            {
                var result = Connection.ControlTransfer((UsbAddressing)ReqTypeHostToDevice, ResetRequest,
                    ResetPurgeTx, PortNumber + 1, null, 0, UsbWriteTimeoutMilliseconds);
                if (result != 0)
                {
                    throw new IOException("Flushing RX failed: result=" + result);
                }
            }

            return true;
        }
    }

    public static ImmutableDeviceList GetSupportedDevices()
    {
        return new ImmutableDeviceList(new Dictionary<int, int[]>
        {
            {
                UsbId.VENDOR_FTDI, [
                    UsbId.FTDI_FT232R,
                    UsbId.FTDI_FT232H,
                    UsbId.FTDI_FT2232H,
                    UsbId.FTDI_FT4232H,
                    UsbId.FTDI_FT231X // same ID for FT230X, FT231X, FT234XD
                ]
            }
        });
    }
}
#endif