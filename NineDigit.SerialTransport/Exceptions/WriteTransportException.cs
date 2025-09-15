// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable ConvertToPrimaryConstructor
namespace NineDigit.SerialTransport;

/// <summary>
/// Represents exception related to writing data to device.
/// </summary>
public class WriteTransportException : TransportException
{
    public WriteTransportException()
        : this(GetDefaultMessage())
    {
    }
        
    public WriteTransportException(string? message)
        : this(portName: null, message)
    {
    }

    public WriteTransportException(string? portName, string? message)
        : this(portName, message, innerException: null)
    {
    }

    public WriteTransportException(string? portName, Exception? innerException)
        : this(portName, GetDefaultMessage(portName), innerException)
    {
    }
        
    public WriteTransportException(string? portName, string? message, Exception? innerException)
        : base(portName, message, innerException)
    {
    }
        
    private static string GetDefaultMessage(string? portName = null)
        => !string.IsNullOrWhiteSpace(portName)
            ? $"Write operation to port '{portName}' failed."
            : "Write operation failed.";
}