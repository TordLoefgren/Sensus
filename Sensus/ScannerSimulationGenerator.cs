using System.Runtime.CompilerServices;

namespace Sensus
{
    public static class ScannerSimulationGenerator
    {
        public static async IAsyncEnumerable<RangeSample> IncreasingRangeCyclic(
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            // https://www.c-sharpcorner.com/blogs/combining-async-and-yield-in-c-sharp2

            uint roundTripDurationUs = 0;

            while (true)
            {
                await Task.Delay(60, cancellationToken);

                if (roundTripDurationUs >= 35000)
                {
                    roundTripDurationUs = 0;
                }

                roundTripDurationUs += 1000;

                yield return new(roundTripDurationUs);
            }
        }
    }
}
