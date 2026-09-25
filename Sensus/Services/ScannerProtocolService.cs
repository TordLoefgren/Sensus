using System.Diagnostics;
using System.IO;
using Sensus.Models;

namespace Sensus.Services
{
    public class ScannerProtocolService : IScannerProtocolService
    {
        private const string ProtocolHandshake = "HELLO";
        private const string ProtocolHandshakeResponse = "HELLO BACK";
        private const string ProtocolStartScanner = "START";

        public async Task<ScannerHandshakeResult> PerformHandshakeAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync(ProtocolHandshake.AsMemory(), cancellationToken);

            var timeout = TimeSpan.FromSeconds(3);
            const string timeoutMessage = "Scanner handshake timed out. Check the connection and try again.";
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                string? response;
                try
                {
                    response = await reader
                        .ReadLineAsync(cancellationToken)
                        .AsTask()
                        .WaitAsync(remaining, cancellationToken);
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException(timeoutMessage, ex);
                }

                if (response is null)
                {
                    throw new EndOfStreamException("The scanner disconnected during the handshake.");
                }

                if (response == ProtocolHandshakeResponse)
                {
                    // This is a harcoded placeholder for the future handshake response.
                    return new(
                        new(
                            "Sensus Rover", "Mk. 1-A",
                            new("ELEGOO UNO R3", "ATmega328"),
                            new("HC-SR04", 2.0, 400.0, 15.0),
                            new("SG90", 180.0)),
                        ScannerConfiguration.Default
                    );
                }

                // We ignore stale or pre-handshake inputs.
            }

            throw new TimeoutException(timeoutMessage);
        }

        public async Task StartScannerAsync(StreamWriter writer, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync(ProtocolStartScanner.AsMemory(), cancellationToken);
        }
    }
}
