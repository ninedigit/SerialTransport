using Microsoft.Extensions.Logging;
using NineDigit.SerialTransport;
using System.Threading;

string? portName = "/dev/cu.usbmodem205C377548521";

SerialPortOptions serialPortOptions = new SerialPortOptions
{
    Parity = Parity.None,
    BaudRate = 9600,
    DataBits = 8
};

ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Trace);
});

ILogger logger = loggerFactory.CreateLogger<Program>();
ISerialPortDeviceSelector deviceSelector = new SerialPortDevicePortNameSelector(portName);
ITransport serialTransport = Transport.Create(deviceSelector, serialPortOptions);

// Read CHDU Lite status
byte[] dataToSend = new byte[] { 0x02, 0x01, 0x00, 0x5A, 0x04 };

byte[] dataReceived = await serialTransport.WriteAndReadAsync(dataToSend, responseLength: 29, CancellationToken.None);

logger.LogDebug("Response received from device: {dataReceived}", dataReceived);
