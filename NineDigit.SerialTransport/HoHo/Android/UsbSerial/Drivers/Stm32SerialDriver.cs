#if ANDROID
using Android.Hardware.Usb;
using Android.Util;

using Java.Nio;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMethodReturnValue.Local

namespace Hoho.Android.UsbSerial.Drivers;

public class Stm32SerialDriver : UsbSerialDriverBase
{
	private const string Tag = nameof(Stm32SerialDriver);

	private int _ctrlInterface;

	public Stm32SerialDriver(UsbDevice device)
	{
		Device = device;
		Port = new Stm32SerialPort(device, 0, this);
	}

	public override UsbDevice Device { get; }
	public override UsbSerialPort Port { get; }

	public static ImmutableDeviceList GetSupportedDevices()
	{
		return new ImmutableDeviceList(new Dictionary<int, int[]>
		{
			{
				UsbId.VENDOR_STM, [
					UsbId.STM32_STLINK,
					UsbId.STM32_VCOM
				]
			}
		});
	}

	public class Stm32SerialPort : CommonUsbSerialPort
	{
		private readonly Stm32SerialDriver _driver;
		private readonly bool _enableAsyncReads;
		private UsbInterface? _controlInterface;
		private UsbInterface? _dataInterface;

		private UsbEndpoint? _readEndpoint;
		private UsbEndpoint? _writeEndpoint;

		private bool _rts;
		private bool _dtr;

		private const int UsbWriteTimeoutMilliseconds = 5000;

		private const int UsbRecipInterface = 0x01;
		private const int UsbRtAm = UsbConstants.UsbTypeClass | UsbRecipInterface;

		private const int SetLineCoding = 0x20; // USB CDC 1.1 section 6.2
		private const int SetControlLineState = 0x22;

		public Stm32SerialPort(UsbDevice device, int portNumber, Stm32SerialDriver driver) : base(device, portNumber)
		{
			_driver = driver;
			_enableAsyncReads = true;
		}

		public override IUsbSerialDriver Driver
			=> _driver;

		private int SendAcmControlMessage(int request, int value, byte[]? buf)
			=> EnsureConnection().ControlTransfer((UsbAddressing)UsbRtAm, request, value, _driver._ctrlInterface, buf,
				buf?.Length ?? 0, UsbWriteTimeoutMilliseconds);

		public override void Open(UsbDeviceConnection connection)
		{
			if (Connection != null)
				throw new IOException("Already opened.");

			Connection = connection;
			
			var opened = false;
			var controlInterfaceFound = false;
			
			try
			{
				for (var i = 0; i < Device.InterfaceCount; i++)
				{
					_controlInterface = Device.GetInterface(i);
					if(_controlInterface.InterfaceClass == UsbClass.Comm)
					{
						if (!Connection.ClaimInterface(_controlInterface, true))
							throw new IOException("Could not claim control interface");
						_driver._ctrlInterface = _controlInterface.Id;
						controlInterfaceFound = true;
						break;
					}
				}
				if (!controlInterfaceFound)
					throw new IOException("Could not claim control interface");
				for (var i = 0; i < Device.InterfaceCount; i++)
				{
					_dataInterface = Device.GetInterface(i);
					if(_dataInterface.InterfaceClass == UsbClass.CdcData)
					{
						if (!Connection.ClaimInterface(_dataInterface, true))
							throw new IOException("Could not claim data interface");
						_readEndpoint = _dataInterface.GetEndpoint(1);
						_writeEndpoint = _dataInterface.GetEndpoint(0);
						opened = true;
						break;
					}
				}
				if(!opened)
					throw new IOException("Could not claim data interface.");
			}
			finally
			{
				if (!opened)
					Connection = null;
			}
		}

		public override void Close()
		{
			if (Connection == null)
				throw new IOException("Already closed");
			Connection.Close();
			Connection = null;
		}

		public override int Read(byte[] dest, int timeoutMillis)
		{
			var connection = EnsureConnection();
			
			if(_enableAsyncReads)
			{
				var request = new UsbRequest();
				try
				{
					request.Initialize(Connection, _readEndpoint);

					// wrap not work here
					// byte[] is a primitive C# value type and not a Java.Lang.Object reference type
					// when you do ByteBuffer.Wrap (dest), Java has no reference to the actual C# byte[], Java will instead make a copy of the bytes.
					// ByteBuffer buf = ByteBuffer.Wrap(dest);

					var buf = ByteBuffer.AllocateDirect(dest.Length);

					if (!request.Queue(buf, buf.Limit()))
						throw new IOException("Error queuing request");
					
					var response = connection.RequestWait();
					if (response == null)
						throw new IOException("Null response");

					var read = buf.Position();
					if (read > 0)
					{
						// set back buffer position to 0
						buf.Rewind();
						// copy the bytes back
						buf.Get(dest, 0, read);
						return read;
					}

					return 0;
				}
				finally
				{
					request.Close();
				}
			}

			int numBytesRead;
			lock(ReadBufferLock)
			{
				var readAmt = Math.Min(dest.Length, ReadBuffer.Length);
				numBytesRead = connection.BulkTransfer(_readEndpoint, ReadBuffer, readAmt, timeoutMillis);
				if(numBytesRead <= 0)
				{
					// This sucks: we get -1 on timeout, not 0 as preferred.
					// We *should* use UsbRequest, except it has a bug/api oversight
					// where there is no way to determine the number of bytes read
					// in response :\ -- http://b.android.com/28023
					if (timeoutMillis == int.MaxValue)
					{
						// Hack: Special case "~infinite timeout" as an error.
						return -1;
					}

					return 0;
				}
				Array.Copy(ReadBuffer, 0, dest, 0, numBytesRead);
			}
			return numBytesRead;
		}

		public override int Write(byte[] src, int timeoutMilliseconds)
		{
			var offset = 0;
			var connection = EnsureConnection();
			
			while(offset < src.Length)
			{
				int writeLength;
				int amtWritten;

				lock(WriteBufferLock)
				{
					writeLength = Math.Min(src.Length - offset, WriteBuffer.Length);

					/*
					//byte[] writeBuffer;
					if (offset == 0)
						writeBuffer = src;
					else
					{
						Array.Copy(src, offset, mWriteBuffer, 0, writeLength);
						writeBuffer = mWriteBuffer;
					}

					amtWritten = mConnection.BulkTransfer(mWriteEndpoint, writeBuffer, writeLength, timeoutMillis);
					*/
					// Issue#36 The bulkTransfer supports offsets
					amtWritten = connection.BulkTransfer(_writeEndpoint, src, offset, writeLength, timeoutMilliseconds);
				}
				if(amtWritten <= 0)
					throw new IOException($"Error writing {writeLength} bytes at offset {offset} length={src.Length}");

				Log.Debug(Tag, $"Wrote amt={amtWritten} attempted={writeLength}");
				offset += amtWritten;
			}

			return offset;
		}

		public override void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
		{
			byte stopBitsBytes;
			switch(stopBits)
			{
				case StopBits.One: 
					stopBitsBytes = 0;
					break;
				case StopBits.OnePointFive:
					stopBitsBytes = 1;
					break;
				case StopBits.Two:
					stopBitsBytes = 2;
					break;
				default:
					throw new ArgumentException($"Bad value for stopBits: {stopBits}");
			}

			byte parityBitesBytes;
			switch(parity)
			{
				case Parity.None:
					parityBitesBytes = 0;
					break;
				case Parity.Odd:
					parityBitesBytes = 1;
					break;
				case Parity.Even:
					parityBitesBytes = 2;
					break;
				case Parity.Mark:
					parityBitesBytes = 3;
					break;
				case Parity.Space:
					parityBitesBytes = 4;
					break;
				default:
					throw new ArgumentException($"Bad value for parity: {parity}");
			}

			byte[] msg = {
				(byte)(baudRate & 0xff),
				(byte) ((baudRate >> 8 ) & 0xff),
				(byte) ((baudRate >> 16) & 0xff),
				(byte) ((baudRate >> 24) & 0xff),
				stopBitsBytes,
				parityBitesBytes,
				(byte) dataBits
			};
			SendAcmControlMessage(SetLineCoding, 0, msg);
		}

		public override bool GetCd() =>
			false; //TODO

		public override bool GetCts() =>
			false; //TODO

		public override bool GetDsr() =>
			false; // TODO

		public override bool GetDtr() =>
			_dtr;

		public override void SetDtr(bool value)
		{
			_dtr = value;
			SetDtrRts();
		}

		public override bool GetRi() =>
			false; //TODO

		public override bool GetRts() =>
			_rts; //TODO

		public override void SetRts(bool value)
		{
			_rts = value;
			SetDtrRts();
		}

		void SetDtrRts()
		{
			var value = (_rts ? 0x2 : 0) | (_dtr ? 0x1 : 0);
			SendAcmControlMessage(SetControlLineState, value, null);
		}
	}
}
#endif