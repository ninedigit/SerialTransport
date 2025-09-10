#if ANDROID

namespace NineDigit.SerialTransport
{
    public sealed class AndroidSerialPortOptions
    {
        public AndroidSerialPortOptions()
        {
        }

        public AndroidSerialPortOptions(SerialPortOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            BaudRate = options.BaudRate;
            Parity = GetParity(options.Parity);
            DataBits = options.DataBits;
            StopBits = GetStopBits(options.StopBits);
            ReadTimeout = options.ReadTimeout;
            WriteTimeout = options.WriteTimeout;
        }

        private Hoho.Android.UsbSerial.Parity GetParity(Parity parity)
        {
            return parity switch
            {
                SerialTransport.Parity.None => Hoho.Android.UsbSerial.Parity.None,
                SerialTransport.Parity.Odd => Hoho.Android.UsbSerial.Parity.Odd,
                SerialTransport.Parity.Even => Hoho.Android.UsbSerial.Parity.Even,
                SerialTransport.Parity.Mark => Hoho.Android.UsbSerial.Parity.Mark,
                SerialTransport.Parity.Space => Hoho.Android.UsbSerial.Parity.Space,
                _ => throw new NotSupportedException()
            };
        }

        private Hoho.Android.UsbSerial.StopBits GetStopBits(StopBit stopBits)
        {
            return stopBits switch
            {
                StopBit.One => Hoho.Android.UsbSerial.StopBits.One,
                StopBit.OnePointFive => Hoho.Android.UsbSerial.StopBits.OnePointFive,
                StopBit.Two => Hoho.Android.UsbSerial.StopBits.Two,
                _ => throw new NotSupportedException()
            };
        }

        public int BaudRate { get; set; } = 115200;
        public int DataBits { get; set; } = 8;
        public Hoho.Android.UsbSerial.Parity Parity { get; set; } = Hoho.Android.UsbSerial.Parity.None;
        public Hoho.Android.UsbSerial.StopBits StopBits { get; set; } = Hoho.Android.UsbSerial.StopBits.One;
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
#endif