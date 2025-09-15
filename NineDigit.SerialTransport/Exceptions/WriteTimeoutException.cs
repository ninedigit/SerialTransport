// ReSharper disable CheckNamespace
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable RedundantIfElseBlock
namespace NineDigit.SerialTransport;

[Serializable]
public class WriteTimeoutException : TransportException
{
    public WriteTimeoutException()
        : this(GetDefaultMessage())
    {
    }
        
    public WriteTimeoutException(string? message)
        : this(timeout: null, portName: null, message)
    {
    }
        
    public WriteTimeoutException(string? message, Exception? innerException)
        : this(timeout: null, portName: null, message, innerException)
    {
    }
        
    public WriteTimeoutException(int? timeout, string? portName)
        : this(timeout, portName, GetDefaultMessage(timeout, portName))
    {
    }

    public WriteTimeoutException(int? timeout, string? portName, string? message)
        : this(timeout, portName, message, innerException: null)
    {
    }

    public WriteTimeoutException(int? timeout, string? portName, Exception? innerException)
        : this(timeout, portName, GetDefaultMessage(timeout, portName), innerException)
    {
        Timeout = timeout;
    }
        
    public WriteTimeoutException(int? timeout, string? portName, string? message, Exception? innerException)
        : base(portName, message, innerException)
    {
        Timeout = timeout;
    }
        
    public int? Timeout { get; }

    public static string GetDefaultMessage(int? timeout = null, string? portName = null)
    {
        if (timeout.HasValue && string.IsNullOrWhiteSpace(portName))
            return $"Write operation for port '{portName}' timed-out after {timeout}.";
        else if (timeout.HasValue && !string.IsNullOrWhiteSpace(portName))
            return $"Write operation timed-out after {timeout}.";
        else if (!timeout.HasValue && string.IsNullOrWhiteSpace(portName))
            return $"Write operation for port '{portName}' timed-out.";
        else
            return "Write operation timed-out.";
    }
}