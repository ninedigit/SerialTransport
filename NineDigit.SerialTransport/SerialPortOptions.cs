// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
namespace NineDigit.SerialTransport;

public sealed class SerialPortOptions
{
    public static readonly int DefaultBaudRate = 115200;
    public static readonly int DefaultDataBits = 8;
    public static readonly Parity DefaultParity = Parity.None;
    public static readonly StopBits DefaultStopBits = StopBits.One;
    public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan DefaultWriteTimeout = TimeSpan.FromMilliseconds(500);

    public int BaudRate { get; set; } = DefaultBaudRate;
    public int DataBits { get; set; } = DefaultDataBits;
    public Parity Parity { get; set; } = DefaultParity;
    public StopBits StopBits { get; set; } = DefaultStopBits;
    public TimeSpan ReadTimeout { get; set; } = DefaultReadTimeout;
    public TimeSpan WriteTimeout { get; set; } = DefaultWriteTimeout;
}