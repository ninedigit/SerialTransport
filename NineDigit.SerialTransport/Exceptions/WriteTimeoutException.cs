using System.Runtime.Serialization;

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

    protected WriteTimeoutException(SerializationInfo info, StreamingContext context)
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