#if ANDROID
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using Hoho.Android.UsbSerial.Drivers;

namespace NineDigit.SerialTransport
{
    internal static class UsbSerialPortExtensions
    {
        public static byte[] Read(this UsbSerialPort self, long length, int timeoutMilliseconds, ILogger logger)
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
                    logger.LogWarning("No time to read remaining data from serial port");

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

                logger.LogDebug(
                    "Reading from Serial Port. Length: {Length} \t BytesRead: {BytesRead} \t TotalBytesRead: {TotalBytesRead}",
                    length, bytesRead, totalBytesRead);
            }

            if (totalBytesRead != length)
                throw new InvalidOperationException($"Incomplete message received. Expected bytes: {length}, received bytes: {totalBytesRead}.");

            return result;
        }
    }
}
#endif