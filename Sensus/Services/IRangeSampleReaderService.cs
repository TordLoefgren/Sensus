using System.IO;
using Sensus.Models;

namespace Sensus.Services
{
    public interface IRangeSampleReaderService
    {
        IAsyncEnumerable<RangeSample> ReadSamplesAsync(StreamReader reader, CancellationToken cancellationToken);
    }
}
