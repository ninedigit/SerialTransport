// ReSharper disable UnusedMember.Global
namespace NineDigit.SerialTransport;

public class SerialPortErrorEventArgs(string? errorType = null, string? errorCode = null, string? message = null, Exception? exception = null)
{
    public string? ErrorType { get; } = errorType;
    public string? ErrorCode { get; } = errorCode;
    public string? Message { get; } = message;
    public Exception? Exception { get; } = exception;
}