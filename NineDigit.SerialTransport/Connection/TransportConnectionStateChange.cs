// ReSharper disable CheckNamespace
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace NineDigit.SerialTransport;

/// <summary>
/// Represents connection state change.
/// </summary>
public sealed class TransportConnectionStateChange
{
    /// <summary>
    /// Creates new instance of connection state change.
    /// </summary>
    /// <param name="newState"></param>
    /// <param name="oldState"></param>
    public TransportConnectionStateChange(TransportConnectionState newState, TransportConnectionState oldState)
    {
        NewState = newState;
        OldState = oldState;
    }

    /// <summary>
    /// Current state
    /// </summary>
    public TransportConnectionState NewState { get; }
    /// <summary>
    /// Previous state.
    /// </summary>
    public TransportConnectionState OldState { get; }
}