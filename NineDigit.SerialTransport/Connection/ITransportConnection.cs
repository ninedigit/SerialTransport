namespace NineDigit.SerialTransport;

/// <summary>
/// Stav transportnej vrstvy.
/// </summary>
public interface ITransportConnection
{
    /// <summary>
    /// Aktuálny stav spojenia transportnej vrstvy.
    /// </summary>
    TransportConnectionState State { get; }

    /// <summary>
    /// Posledná chyba, ktorá spôsobila prerušenie spojenia.
    /// Hodnota je nastavená iba pre <see cref="State"/> rovný <see cref="TransportConnectionState.Disconnected"/>.
    /// </summary>
    /// <value><see cref="Exception"/> alebo <c>null</c>.</value>
    Exception? LastError { get; }

    /// <summary>
    /// Raised when transport error occurs.
    /// </summary>
#pragma warning disable CA1716 // Identifiers should not match keywords
    event EventHandler<ErrorEventArgs> Error;
#pragma warning restore CA1716 // Identifiers should not match keywords

    /// <summary>
    /// Raised when trasnport connection state is changed.
    /// </summary>
    event EventHandler<TransportConnectionStateChange> StateChanged;
}