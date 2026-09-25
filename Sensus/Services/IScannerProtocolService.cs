using System.IO;
using Sensus.Models;

namespace Sensus.Services
{
    public interface IScannerProtocolService
    {
        Task<ScannerHandshakeResult> PerformHandshakeAsync(StreamReader reader, StreamWriter writer, CancellationToken cancellationToken);

        Task StartScannerAsync(StreamWriter writer, CancellationToken cancellationToken);
    }
}
