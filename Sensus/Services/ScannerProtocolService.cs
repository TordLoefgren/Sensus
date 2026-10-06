using System.Diagnostics;
using System.IO;
using Sensus.Models;
using Sensus.Protocol;
using Sensus.Serializers;

namespace Sensus.Services
{
    public class ScannerProtocolService : IScannerProtocolService
    {
        public async Task<ScannerHandshakeResponse> PerformHandshakeAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync(ScannerProtocolMessages.Prepare.AsMemory(), cancellationToken);

            var timeout = TimeSpan.FromSeconds(3);
            const string timeoutMessage = "Scanner handshake timed out. Check the connection and try again.";
            var stopwatch = Stopwatch.StartNew();

            var lines = new List<string>(ScannerHandshakeResponseSerializer.LineCount);

            while (true)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(timeoutMessage);
                }

                string? line;
                try
                {
                    line = await reader
                        .ReadLineAsync(cancellationToken)
                        .AsTask()
                        .WaitAsync(remaining, cancellationToken);
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException(timeoutMessage, ex);
                }

                if (line is null)
                {
                    throw new EndOfStreamException("The scanner disconnected during the handshake.");
                }

                if (lines.Count == 0)
                {
                    if (line == ScannerProtocolMessages.Stopped)
                    {
                        // A STOPPED response from an earlier run may still be buffered.
                        continue;
                    }

                    if (line.StartsWith("SENSUS,", StringComparison.Ordinal) &&
                        line.EndsWith(",DESCRIPTION", StringComparison.Ordinal) &&
                        line != ScannerProtocolMessages.Description
                    )
                    {
                        throw new InvalidDataException("The scanner uses an unsupported protocol revision.");
                    }

                    if (line != ScannerProtocolMessages.Description)
                    {
                        // Ignore stale input until the handshake response begins.
                        continue;
                    }
                }

                lines.Add(line);

                if (line == ScannerProtocolMessages.Ready)
                {
                    // ReadLineAsync removes line endings; restore the serializer's separator.
                    var response = string.Join("\r\n", lines);
                    if (!ScannerHandshakeResponseSerializer.TryDeserialize(response, out var handshake))
                    {
                        throw new InvalidDataException("The scanner sent an invalid handshake response.");
                    }

                    return handshake;
                }

                if (lines.Count == ScannerHandshakeResponseSerializer.LineCount)
                {
                    throw new InvalidDataException("The scanner handshake did not end with READY.");
                }
            }
        }

        public async Task StartScannerAsync(StreamWriter writer, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync(ScannerProtocolMessages.Start.AsMemory(), cancellationToken);
        }

        public async Task StopScannerAsync(StreamWriter writer, CancellationToken cancellationToken)
        {
            await writer.WriteLineAsync(ScannerProtocolMessages.Stop.AsMemory(), cancellationToken);
        }
    }
}
