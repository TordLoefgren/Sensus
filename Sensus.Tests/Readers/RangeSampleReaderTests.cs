using System.Text;
using Sensus.Models;
using Sensus.Readers;

namespace Sensus.Tests.Readers
{
    public class RangeSampleReaderTests
    {
        [Fact]
        public async Task ReadSamplesAsync_InvokesCallbackOnlyForProtocolMessages_When_InputContainsNonSampleLines()
        {
            // Arrange.
            const string input =
                "noise\r\n" +
                "1,1,0,0,1000,0\r\n" +
                "SENSUS,1,STOPPED\r\n" +
                "1,1,invalid\r\n" +
                "SENSUS,2,STOPPED\r\n";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var reader = new StreamReader(stream);
            var protocolMessages = new List<string>();
            var samples = new List<RangeSample>();

            // Act.
            await foreach (
                var sample in RangeSampleReader.ReadSamplesAsync(
                    reader,
                    CancellationToken.None,
                    protocolMessages.Add
                )
            )
            {
                samples.Add(sample);
            }

            // Assert.
            Assert.Single(samples);
            Assert.Equal((uint)1000, samples[0].RoundTripDurationUs);
            Assert.Equal(["SENSUS,1,STOPPED", "SENSUS,2,STOPPED"], protocolMessages);
        }
    }
}
