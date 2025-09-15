#if ANDROID
using System.Diagnostics;
using Hoho.Android.UsbSerial.Drivers;
// ReSharper disable CheckNamespace

namespace NineDigit.SerialTransport
{
    internal static class UsbSerialPortExtensions
    {
        public static byte[] Read(this UsbSerialPort self, long length, int timeoutMilliseconds)
        {
            if (length == 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Positive non-zero length expected.");
            if (timeoutMilliseconds == 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Positive non-zero length expected.");

            var stopWatch = Stopwatch.StartNew();
            var result = new byte[length];
            var buffer = new byte[length];
            long totalBytesRead = 0;

            while (totalBytesRead < length)
            {
                var readTimeoutMilliseconds = (int)Math.Min(int.MaxValue, Math.Max(0, timeoutMilliseconds - stopWatch.ElapsedMilliseconds));
                if (readTimeoutMilliseconds == 0)
                    SerialTransportEventSource.Log.ReadTimeoutElapsed(timeoutMilliseconds, self.Driver.Device.DeviceName);

                var bytesRead = self.Read(buffer, readTimeoutMilliseconds);
                if (bytesRead == 0)
                    break;

                var destinationIndex = totalBytesRead;

                totalBytesRead += bytesRead;
                if (totalBytesRead > length)
                    throw new InvalidOperationException($"Unexpected response length received. Expected length {length}, received {totalBytesRead} bytes.");

                Array.Copy(
                    sourceArray: buffer, 
                    sourceIndex: 0,
                    destinationArray: result,
                    destinationIndex: destinationIndex,
                    length: bytesRead);

                SerialTransportEventSource.Log.ReadingDataChunk(length, bytesRead, totalBytesRead, self.Driver.Device.DeviceName);
            }

            if (totalBytesRead != length)
                throw new InvalidOperationException($"Incomplete message received. Expected bytes: {length}, received bytes: {totalBytesRead}.");

            return result;
        }
    }
}
#endif