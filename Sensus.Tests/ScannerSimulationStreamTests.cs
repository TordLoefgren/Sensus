using System.Text;
using Sensus.Services;

namespace Sensus.Tests
{
    public class ScannerSimulationStreamTests
    {
        private readonly IRangeSampleSerializerService _rangeSampleSerializerService;

        public ScannerSimulationStreamTests()
        {
            _rangeSampleSerializerService = new RangeSampleSerializerService();
        }

        private static async IAsyncEnumerable<RangeSample> Samples(
            params uint[] values
        )
        {
            foreach (var value in values)
            {
                yield return new(value);
                await Task.Yield();
            }
        }

        [Fact]
        public async Task ReadAsync__ReturnsCount_WhenCountIsSmallerThanPayload()
        {
            // Arrange
            var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );
            var buffer = new byte[8];

            // Act
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(0, 2),
                CancellationToken.None
            );

            var decodedText = Encoding.UTF8.GetString(buffer[..2]);

            // Assert
            Assert.Equal(2, bytesRead);
            Assert.Equal("10", decodedText);
        }

        [Fact]
        public async Task ReadAsync_ContinuesSamePayload_AfterShortRead()
        {
            // Arrange
            var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );

            // Act
            var first = new byte[2];
            var second = new byte[4];

            var firstCount = await stream.ReadAsync(first.AsMemory(0, 2), CancellationToken.None);
            var secondCount = await stream.ReadAsync(second.AsMemory(0, 4), CancellationToken.None);

            var firstDecodedText = Encoding.UTF8.GetString(first);
            var secondDecodedText = Encoding.UTF8.GetString(second);

            // Assert
            Assert.Equal(2, firstCount);
            Assert.Equal(4, secondCount);
            Assert.Equal("10", firstDecodedText);
            Assert.Equal("00\r\n", secondDecodedText);
        }

        [Fact]
        public async Task ReadAsync_RespectsOffset()
        {
            // Arrange
            var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );
            byte sentinelValue = 0xCC;
            var buffer = Enumerable.Repeat(sentinelValue, 10).ToArray();

            // Act
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(3, 2),
                CancellationToken.None
            );

            // Assert
            Assert.Equal(2, bytesRead);
            Assert.Equal(sentinelValue, buffer[0]);
            Assert.Equal(sentinelValue, buffer[1]);
            Assert.Equal(sentinelValue, buffer[2]);
            Assert.NotEqual(sentinelValue, buffer[3]);
            Assert.NotEqual(sentinelValue, buffer[4]);
            Assert.Equal(sentinelValue, buffer[5]);
            Assert.Equal(sentinelValue, buffer[6]);
            Assert.Equal(sentinelValue, buffer[7]);
            Assert.Equal(sentinelValue, buffer[8]);
            Assert.Equal(sentinelValue, buffer[9]);
        }

        [Fact]
        public async Task ReadAsync_ThrowsCancellation_WhenCancelled()
        {
            var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var buffer = new byte[8];

            // Successful completion is not expected in this cancellation test.

#pragma warning disable CA2022

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await stream.ReadAsync(buffer.AsMemory(), cancellationTokenSource.Token)
            );
#pragma warning restore CA2022

        }

        [Fact]
        public async Task ReadAsync_PreservesByteSequence_AcrossAwkwardReadSizes()
        {
            // Arrange
            var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );

            var readSizes = new[] { 1, 3, 2, 4, 2, 6 };
            var receivedBytes = new List<byte>();

            // Act
            foreach (var readSize in readSizes)
            {
                var buffer = new byte[readSize];

                var bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(),
                    CancellationToken.None
                );

                receivedBytes.AddRange(buffer[..bytesRead]);
            }

            var decodedText = Encoding.UTF8.GetString(receivedBytes.ToArray());

            // Assert
            Assert.Equal("1000\r\n2000\r\n3000\r\n", decodedText);
        }
    }
}
