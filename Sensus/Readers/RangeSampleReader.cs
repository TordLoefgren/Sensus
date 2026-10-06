using System.IO;
using System.Runtime.CompilerServices;
using Sensus.Models;
using Sensus.Serializers;

namespace Sensus.Readers
{
    public static class RangeSampleReader
    {
        public static async IAsyncEnumerable<RangeSample> ReadSamplesAsync(
            StreamReader reader,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            Action<string>? onProtocolMessageCallback = null
        )
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (RangeSampleSerializer.TryDeserialize(line, out var sample))
                {
                    yield return sample;
                }
                else if (line.StartsWith("SENSUS,", StringComparison.Ordinal))
                {
                    onProtocolMessageCallback?.Invoke(line);
                }
            }
        }
    }
}
