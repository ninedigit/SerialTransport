namespace NineDigit.SerialTransport;

/// <summary>
/// Base implementation of transport connection
/// </summary>
public abstract class TransportConnectionBase : ITransportConnection, IDisposable
{
    /// <summary>
    /// Raised when transport error occurs.
    /// </summary>
    public event EventHandler<ErrorEventArgs>? Error;
    /// <summary>
    /// Raised when trasnport connection state is changed.
    /// </summary>
    public event EventHandler<TransportConnectionStateChange>? StateChanged;

    /// <summary>
    /// Current transport connection state.
    /// </summary>
    public TransportConnectionState State { get; private set; }

    /// <summary>
    /// Last error that caused disconnecting of transport connection.
    /// Value is non-null only when <see cref="State"/> is equal to <see cref="TransportConnectionState.Disconnected"/>.
    /// </summary>
    /// <value><see cref="Exception"/> or <c>null</c>.</value>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Sets connection state to <see cref="TransportConnectionState.Connected"/> and clears last error.
    /// </summary>
    protected void SetConnected()
    {
        LastError = null;
        SetState(TransportConnectionState.Connected);
    }

    /// <summary>
    /// Sets connection state to <see cref="TransportConnectionState.Disconnected"/> and raises the <see cref="Error"/> event.
    /// </summary>
    protected void SetDisconnected()
    {
        LastError = null;
        SetState(TransportConnectionState.Disconnected);
    }

    /// <summary>
    /// Sets connection state to <see cref="TransportConnectionState.Disconnected"/>, stores given <paramref name="ex"/> and raises the <see cref="Error"/> event.
    /// </summary>
    /// <param name="ex"></param>
    protected void SetDisconnected(Exception ex)
    {
        RaiseError(ex);
        SetState(TransportConnectionState.Disconnected);
    }

    private void SetState(TransportConnectionState state)
    {
        var oldState = State;
        if (oldState != state)
        {
            State = state;
            var eventArg = new TransportConnectionStateChange(state, oldState);

            StateChanged?.Invoke(this, eventArg);
        }
    }

    private void RaiseError(Exception ex)
    {
        LastError = ex ?? throw new ArgumentNullException(nameof(ex));
        var errEventArgs = new ErrorEventArgs(ex);
        Error?.Invoke(this, errEventArgs);
    }

    #region IDisposable
    bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
            
        if (disposing)
            SetDisconnected();

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}