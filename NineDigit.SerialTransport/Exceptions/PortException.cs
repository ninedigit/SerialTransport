// ReSharper disable CheckNamespace
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedMember.Global
namespace NineDigit.SerialTransport;

public class PortException : Exception
{
    public PortException()
        : this(message: null)
    {
    }

    public PortException(string? message)
        : this(message, innerException: null)
    {
    }

    public PortException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
        
    public PortException(string? portName, string? message)
        : base(message)
    {
        PortName = portName;
    }

    public PortException(string? portName, string? message, Exception? innerException)
        : base(message, innerException)
    {
        PortName = portName;
    }
        
    public string? PortName { get; }
}