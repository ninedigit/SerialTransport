namespace NineDigit.SerialTransport
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class SerialPortOptions
    {
        public int BaudRate { get; set; } = 115200;
        public Parity Parity { get; set; } = Parity.None;
        public int DataBits { get; set; } = 8;
        public StopBit StopBits { get; set; } = StopBit.One;

        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
