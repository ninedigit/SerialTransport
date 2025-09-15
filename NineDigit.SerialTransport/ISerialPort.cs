namespace NineDigit.SerialTransport
{
    public interface ISerialPort : IDisposable
    {
        event EventHandler<SerialPortErrorEventArgs> OnError;
        
        string Name { get; }
        
        int ReadTimeout { get; set; }
        int WriteTimeout { get; set; }

        Task OpenAsync(CancellationToken cancellationToken = default);
        void Write(byte[] data);
        byte[] Read(int responseLength);
        void DiscardBuffers();
        void Close();
    }
}
