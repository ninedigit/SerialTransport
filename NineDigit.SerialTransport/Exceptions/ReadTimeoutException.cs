using System.Runtime.Serialization;

namespace NineDigit.SerialTransport;

[Serializable]
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

    protected ReadTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        foreach (var entry in info)
        {
            switch (entry.Name)
            {
                case "Timeout":
                    Timeout = (int)entry.Value;
                    break;
            }
        }
    }
        
    public int? Timeout { get; }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
            
        if (Timeout.HasValue)
            info.AddValue("Timeout", Timeout.Value);
    }
        
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