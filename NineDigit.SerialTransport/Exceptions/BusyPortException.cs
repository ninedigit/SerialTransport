using System.Runtime.Serialization;

namespace NineDigit.SerialTransport;

[Serializable]
public class BusyPortException : PortException
{
    public BusyPortException()
        : this(message: null)
    {
    }
        
    public BusyPortException(string? message)
        : this(message, innerException: null)
    {
    }

    public BusyPortException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
        
    public BusyPortException(string? portName, string? message)
        : base(portName, message)
    {
    }

    public BusyPortException(string? portName, string? message, Exception? innerException)
        : base(portName, message, innerException)
    {
    }
        
    protected BusyPortException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}