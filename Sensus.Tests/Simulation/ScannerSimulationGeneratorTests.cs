using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Simulation;

namespace Sensus.Tests.Simulation
{
    public class ScannerSimulationGeneratorTests
    {
        [Fact]
        public async Task NoEchoRangeSweep_AlternatesStatuses_When_CrossingIntervals_And_ReversingDirection()
        {
            // Arrange.
            var configuration = new ScannerConfiguration(-90, 90, 12, 1, 30_000);
            var samples = new List<RangeSample>();

            // Act.
            await foreach (
                var sample in ScannerSimulationGenerator.NoEchoRangeSweep(
                    configuration,
                    CancellationToken.None
                )
            )
            {
                samples.Add(sample);
                if (samples.Count == 32)
                {
                    break;
                }
            }

            // Assert.
            Assert.Equal(
                [
                    SampleStatus.Valid,
                    SampleStatus.NoEcho,
                    SampleStatus.Valid,
                    SampleStatus.NoEcho,
                    SampleStatus.Valid
                ],
                samples.Take(15).Chunk(3).Select(band => band.Select(sample => sample.Status).Distinct().Single())
            );
            Assert.Equal(SampleStatus.Valid, samples[15].Status);
            Assert.Equal(
                [
                    SampleStatus.NoEcho,
                    SampleStatus.NoEcho,
                    SampleStatus.NoEcho,
                    SampleStatus.Valid,
                    SampleStatus.Valid
                ],
                samples.Skip(16).Take(5).Select(sample => sample.Status)
            );
            Assert.Equal(SampleStatus.NoEcho, samples[30].Status);
            Assert.Equal(SampleStatus.Valid, samples[31].Status);
            Assert.All(samples, sample =>
                Assert.Equal(
                    sample.Status == SampleStatus.Valid ? 14_000u : 0u,
                    sample.RoundTripDurationUs
                )
            );
        }
    }
}
