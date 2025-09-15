// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable IntroduceOptionalParameters.Global
namespace NineDigit.SerialTransport;

public class ReadTimeoutException : TransportException
{
    public ReadTimeoutException()
        : this(timeout: null, portName: null)
    {
    }

    public ReadTimeoutException(string? message)
        : this(timeout: null, portName: null, message)
    {
    }
        
    public ReadTimeoutException(string? message, Exception? innerException)
        : this(timeout: null, portName: null, message, innerException)
    {
    }
        
    /// <summary>
    /// Creates new instance of transport exception.
    /// </summary>
    public ReadTimeoutException(int? timeout, string? portName)
        : this(timeout, portName, GetDefaultMessage(timeout, portName))
    {
    }

    public ReadTimeoutException(int? timeout, string? portName, string? message)
        : this(timeout, portName, message, innerException: null)
    {
    }

    public ReadTimeoutException(int? timeout, string? portName, Exception? innerException)
        : this(timeout, portName, GetDefaultMessage(timeout, portName), innerException)
    {
        Timeout = timeout;
    }
        
    public ReadTimeoutException(int? timeout, string? portName, string? message, Exception? innerException)
        : base(portName, message, innerException)
    {
        Timeout = timeout;
    }
        
    public int? Timeout { get; }

    public static string GetDefaultMessage(int? timeout, string? portName)
    {
        if (timeout.HasValue && string.IsNullOrWhiteSpace(portName))
            return $"Read operation for port '{portName}' timed-out after {timeout}.";
        else if (timeout.HasValue && !string.IsNullOrWhiteSpace(portName))
            return $"Read operation timed-out after {timeout}.";
        else if (!timeout.HasValue && string.IsNullOrWhiteSpace(portName))
            return $"Read operation for port '{portName}' timed-out.";
        else
            return "Read operation timed-out.";
    }
}