using System.Diagnostics.Tracing;

namespace NineDigit.SerialTransport;

[EventSource(Name = "NineDigit.SerialTransport.EventSource")]
internal class SerialTransportEventSource : EventSource
{
    public static readonly SerialTransportEventSource Log = new();
    
    public class Keywords
    {
        public const EventKeywords TransportFactory = (EventKeywords)0x1;
        public const EventKeywords SerialPort = (EventKeywords)0x2;
    }
    
    // [Event(1, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
    //     Message = "HTTP {method} {path} -> {status} in {elapsedMs}ms")]
    // public void HttpRequest(string method, string path, int status, long elapsedMs)
    //     => WriteEvent(1, method, path, status, elapsedMs);

    [Event(1, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Opening Serial Port for device {deviceName}")]
    public void OpeningSerialPortDevice(string deviceName)
        => WriteEvent(1, deviceName);
    
    [Event(2, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Opening Serial Port connection for device {deviceName}")]
    public void OpeningSerialPortDeviceConnection(string deviceName)
        => WriteEvent(2, deviceName);
    
    [Event(3, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Writing {bytesCount} bytes of data to Serial Port device {deviceName}")]
    public void WritingData(int bytesCount, string deviceName)
        => WriteEvent(3, bytesCount, deviceName);
    
    [Event(4, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Writing {bytesCount} bytes of data from Serial Port device {deviceName}")]
    public void ReadingData(int bytesCount, string deviceName)
        => WriteEvent(4, bytesCount, deviceName);
    
    [Event(5, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Discarding Serial Port buffers for device {deviceName}")]
    public void DiscardingBuffers(string deviceName)
        => WriteEvent(5, deviceName);
    
    [Event(6, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Closing Serial Port connection for device {deviceName}")]
    public void ClosingSerialPortDeviceConnection(string deviceName)
        => WriteEvent(6, deviceName);
    
    [Event(7, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Opening Serial Port for device {deviceName}")]
    public void DisposingSerialPortDevice(string deviceName)
        => WriteEvent(7, deviceName);
    
    [Event(8, Level = EventLevel.Warning, Keywords = Keywords.SerialPort,
        Message = "Read timeout of {timeout}ms elapsed for device {deviceName}")]
    public void ReadTimeoutElapsed(int timeout, string deviceName)
        => WriteEvent(8, timeout, deviceName);
    
    [Event(9, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "Read {bytesRead} byte chunk ({totalBytesRead}/{length}) from device {deviceName}")]
    public void ReadingDataChunk(long length, int bytesRead, long totalBytesRead, string deviceName)
        => WriteEvent(9, bytesRead, totalBytesRead, length, deviceName);
    
    [Event(10, Level = EventLevel.Error, Keywords = Keywords.SerialPort,
        Message = "Device {deviceName} was disconnected with error {error}")]
    public void SerialPortDeviceConnectionError(string deviceName, string error)
        => WriteEvent(10, deviceName, error);
    
    [Event(11, Level = EventLevel.Error, Keywords = Keywords.SerialPort,
        Message = "Received Serial Port error event for device {deviceName}. (ErrorType: {errorType}, ErrorCode: {errorCode}, Message: {message}, Exception: {exception})")]
    public void ReceivedSerialPortError(string deviceName, string? errorType, string? errorCode, string? message, string? exception)
        => WriteEvent(11, deviceName, errorType, errorCode, message, exception);
    
    [Event(12, Level = EventLevel.Informational, Keywords = Keywords.SerialPort,
        Message = "USB Serial Port device {deviceName} was detached")]
    public void SerialPortDeviceWasDetached(string deviceName)
        => WriteEvent(12, deviceName);
}