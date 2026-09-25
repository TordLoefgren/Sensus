using System.IO;
using System.Runtime.CompilerServices;
using Sensus.Models;

namespace Sensus.Services
{
    public class RangeSampleReaderService : IRangeSampleReaderService
    {
        private readonly IRangeSampleSerializerService _rangeSampleSerializerService;

        public RangeSampleReaderService(IRangeSampleSerializerService rangeSampleSerializerService)
        {
            _rangeSampleSerializerService = rangeSampleSerializerService;
        }

        public async IAsyncEnumerable<RangeSample> ReadSamplesAsync(
            StreamReader reader,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (_rangeSampleSerializerService.TryDeserialize(line, out var sample))
                {
                    yield return sample;
                }
            }
        }
    }
}
