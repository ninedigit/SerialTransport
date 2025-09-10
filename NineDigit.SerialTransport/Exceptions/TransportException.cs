using System.Runtime.Serialization;

namespace NineDigit.SerialTransport;

/// <summary>
/// Represents exception related to data exchange between devices.
/// </summary>
[Serializable]
public class TransportException : PortException
{
    public TransportException()
        : this(message: null)
    {
    }

    public TransportException(string? message)
        : base(message)
    {
    }

    public TransportException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
        
    public TransportException(string? portName, string? message)
        : base(portName, message)
    {
    }

    public TransportException(string? portName, string? message, Exception? innerException)
        : base(portName, message, innerException)
    {
    }

    protected TransportException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}