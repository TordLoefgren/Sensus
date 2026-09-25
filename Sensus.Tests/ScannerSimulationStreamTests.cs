using System.Text;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Services;
using Sensus.Simulation;

namespace Sensus.Tests
{
    public class ScannerSimulationStreamTests
    {
        private readonly IRangeSampleSerializerService _rangeSampleSerializerService;

        public ScannerSimulationStreamTests()
        {
            _rangeSampleSerializerService = new RangeSampleSerializerService();
        }

        private static RangeSample CreateDefaultSample(uint roundTripDurationUs)
        {
            return new(0, 0, 0, 0, roundTripDurationUs, SampleStatus.Valid);
        }

        private static async IAsyncEnumerable<RangeSample> Samples(
            params uint[] values
        )
        {
            foreach (var value in values)
            {
                yield return CreateDefaultSample(value);
                await Task.Yield();
            }
        }

        private static async Task StartScannerAsync(ScannerSimulationStream stream)
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            await writer.WriteLineAsync("HELLO");

            var response = new byte[Encoding.UTF8.GetByteCount("HELLO BACK\r\n")];
            await stream.ReadExactlyAsync(response);

            Assert.Equal("HELLO BACK\r\n", Encoding.UTF8.GetString(response));

            await writer.WriteLineAsync("START");
        }

        [Fact]
        public async Task ReadAsync_ReturnsCount_WhenCountIsSmallerThanPayload()
        {
            // Arrange
            await using var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );
            await StartScannerAsync(stream);
            var buffer = new byte[8];

            // Act
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(0, 2),
                CancellationToken.None
            );

            var decodedText = Encoding.UTF8.GetString(buffer[..2]);

            // Assert
            Assert.Equal(2, bytesRead);
            Assert.Equal("0,", decodedText);
        }

        [Fact]
        public async Task ReadAsync_ContinuesSamePayload_AfterShortRead()
        {
            // Arrange
            await using var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );
            await StartScannerAsync(stream);

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
            Assert.Equal("0,", firstDecodedText);
            Assert.Equal("0,0,", secondDecodedText);
        }

        [Fact]
        public async Task ReadAsync_RespectsOffset()
        {
            // Arrange
            await using var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );
            await StartScannerAsync(stream);
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
            await using var stream = new ScannerSimulationStream(
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
            await using var stream = new ScannerSimulationStream(
                _ => Samples(1000, 2000, 3000),
                _rangeSampleSerializerService
            );
            await StartScannerAsync(stream);

            var readSizes = new[] { 1, 3, 2, 4, 2, 6 };
            var receivedBytes = new List<byte>();

            // Act
            for (var i = 0; ; i++)
            {
                var readSize = readSizes[i % readSizes.Length];
                var buffer = new byte[readSize];

                var bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(),
                    CancellationToken.None
                );

                if (bytesRead == 0)
                {
                    break;
                }

                receivedBytes.AddRange(buffer[..bytesRead]);
            }

            var decodedText = Encoding.UTF8.GetString(receivedBytes.ToArray());

            // Assert
            Assert.Equal("0,0,0,0,1000,0\r\n0,0,0,0,2000,0\r\n0,0,0,0,3000,0\r\n", decodedText);
        }

        [Fact]
        public async Task WriteAsync_ReplacesPartiallyReadHandshakeResponse()
        {
            // Arrange
            await using var stream = new ScannerSimulationStream(
                _ => Samples(1000),
                _rangeSampleSerializerService
            );

            // Exercise the array overload with a command inside a larger buffer.
            var command = Encoding.UTF8.GetBytes("xxHELLO\r\nyy");
            await stream.WriteAsync(command, 2, 7, CancellationToken.None);

            var partialResponse = new byte[3];
            await stream.ReadExactlyAsync(partialResponse);

            // Act
            // A new HELLO replaces the unread reply and starts at its first byte.
            await StartScannerAsync(stream);

            var sample = new byte[Encoding.UTF8.GetByteCount("0,0,0,0,1000,0\r\n")];
            await stream.ReadExactlyAsync(sample);

            // Assert
            Assert.Equal("HEL", Encoding.UTF8.GetString(partialResponse));
            Assert.Equal("0,0,0,0,1000,0\r\n", Encoding.UTF8.GetString(sample));
        }
    }
}
