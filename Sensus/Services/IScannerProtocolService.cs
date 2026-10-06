using System.IO;
using Sensus.Models;

namespace Sensus.Services
{
    public interface IScannerProtocolService
    {
        Task<ScannerHandshakeResponse> PerformHandshakeAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken);

        Task StartScannerAsync(StreamWriter writer, CancellationToken cancellationToken);

        Task StopScannerAsync(StreamWriter writer, CancellationToken cancellationToken);
    }
}
