// ReSharper disable CheckNamespace
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable EventNeverSubscribedTo.Global
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
    event EventHandler<ErrorEventArgs> Error;

    /// <summary>
    /// Raised when trasnport connection state is changed.
    /// </summary>
    event EventHandler<TransportConnectionStateChange> StateChanged;
}