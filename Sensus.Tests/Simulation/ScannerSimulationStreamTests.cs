using System.Text;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Simulation;

namespace Sensus.Tests.Simulation
{
    public class ScannerSimulationStreamTests
    {
        private const string HandshakeResponse =
            "SENSUS,1,DESCRIPTION\r\n" +
            "SCANNER,Sensus Rover,Mk. 1-A\r\n" +
            "BOARD,ELEGOO UNO R3,ATmega328\r\n" +
            "RANGE_SENSOR,HC-SR04,2,400,15\r\n" +
            "SERVO,SG90,180\r\n" +
            "CONFIGURATION,-90,90,1,100,30000\r\n" +
            "SENSUS,1,READY\r\n";

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

        private static async IAsyncEnumerable<RangeSample> SlowSamples(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            yield return CreateDefaultSample(1000);
        }

        private static ScannerSimulationStream CreateStream(params uint[] values)
            => new(_ => Samples(values), HandshakeResponse);

        private static async Task StartScannerAsync(ScannerSimulationStream stream)
        {
            await using var writer = new StreamWriter(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            await writer.WriteLineAsync("SENSUS,1,PREPARE");

            var response = new byte[Encoding.UTF8.GetByteCount(HandshakeResponse)];
            await stream.ReadExactlyAsync(response);

            Assert.Equal(HandshakeResponse, Encoding.UTF8.GetString(response));

            await writer.WriteLineAsync("SENSUS,1,START");
        }

        [Fact]
        public async Task ReadAsync_ReturnsRequestedByteCount_When_BufferIsSmallerThanPayload()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000, 3000);
            await StartScannerAsync(stream);
            var buffer = new byte[8];

            // Act.
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(0, 2),
                CancellationToken.None
            );

            var decodedText = Encoding.UTF8.GetString(buffer[..2]);

            // Assert.
            Assert.Equal(2, bytesRead);
            Assert.Equal("0,", decodedText);
        }

        [Fact]
        public async Task ReadAsync_ContinuesPendingPayload_When_PreviousReadWasShort()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000, 3000);
            await StartScannerAsync(stream);

            // Act.
            var first = new byte[2];
            var second = new byte[4];

            var firstCount = await stream.ReadAsync(first.AsMemory(0, 2), CancellationToken.None);
            var secondCount = await stream.ReadAsync(second.AsMemory(0, 4), CancellationToken.None);

            var firstDecodedText = Encoding.UTF8.GetString(first);
            var secondDecodedText = Encoding.UTF8.GetString(second);

            // Assert.
            Assert.Equal(2, firstCount);
            Assert.Equal(4, secondCount);
            Assert.Equal("0,", firstDecodedText);
            Assert.Equal("0,0,", secondDecodedText);
        }

        [Fact]
        public async Task ReadAsync_WritesWithinRequestedSlice_When_BufferHasOffset()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000, 3000);
            await StartScannerAsync(stream);
            byte sentinelValue = 0xCC;
            var buffer = Enumerable.Repeat(sentinelValue, 10).ToArray();

            // Act.
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(3, 2),
                CancellationToken.None
            );

            // Assert.
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
        public async Task ReadAsync_ThrowsOperationCanceledException_When_TokenIsCancelled()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000, 3000);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var buffer = new byte[8];

            // Successful completion is not expected in this cancellation test.

#pragma warning disable CA2022

            // Act & Assert.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await stream.ReadAsync(buffer.AsMemory(), cancellationTokenSource.Token)
            );

#pragma warning restore CA2022

        }

        [Fact]
        public async Task ReadAsync_PreservesByteSequence_When_ReadSizesVary()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000, 3000);
            await StartScannerAsync(stream);

            var readSizes = new[] { 1, 3, 2, 4, 2, 6 };
            var receivedBytes = new List<byte>();

            // Act.
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

            // Assert.
            Assert.Equal("0,0,0,0,1000,0\r\n0,0,0,0,2000,0\r\n0,0,0,0,3000,0\r\n", decodedText);
        }

        [Fact]
        public async Task WriteAsync_ReplacesUnreadHandshakeResponse_When_NewPrepareArrives()
        {
            // Arrange.
            await using var stream = CreateStream(1000);

            // Exercise the array overload with a command inside a larger buffer.
            var command = Encoding.UTF8.GetBytes("xxSENSUS,1,PREPARE\r\nyy");
            await stream.WriteAsync(command, 2, Encoding.UTF8.GetByteCount("SENSUS,1,PREPARE\r\n"), CancellationToken.None);

            var partialResponse = new byte[3];
            await stream.ReadExactlyAsync(partialResponse);

            // Act.
            // A new PREPARE replaces the unread reply and starts at its first byte.
            await StartScannerAsync(stream);

            var sample = new byte[Encoding.UTF8.GetByteCount("0,0,0,0,1000,0\r\n")];
            await stream.ReadExactlyAsync(sample);

            // Assert.
            Assert.Equal("SEN", Encoding.UTF8.GetString(partialResponse));
            Assert.Equal("0,0,0,0,1000,0\r\n", Encoding.UTF8.GetString(sample));
        }

        [Fact]
        public async Task WriteAsync_EmitsStoppedResponse_When_StopCommandArrives_And_RestartRequiresPrepare()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000);
            await StartScannerAsync(stream);

            await using var writer = new StreamWriter(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            // Act & Assert.
            var samplePrefix = new byte[3];
            await stream.ReadExactlyAsync(samplePrefix);
            Assert.Equal("0,0", Encoding.UTF8.GetString(samplePrefix));

            await writer.WriteLineAsync("SENSUS,1,STOP");

            var remaining = new byte[Encoding.UTF8.GetByteCount(",0,0,1000,0\r\nSENSUS,1,STOPPED\r\n")];
            await stream.ReadExactlyAsync(remaining);
            Assert.Equal(",0,0,1000,0\r\nSENSUS,1,STOPPED\r\n", Encoding.UTF8.GetString(remaining));

            await writer.WriteLineAsync("SENSUS,1,START");
            Assert.Equal(0, await stream.ReadAsync(new byte[1]));

            await writer.WriteLineAsync("SENSUS,1,STOP");
            var stopped = new byte[Encoding.UTF8.GetByteCount("SENSUS,1,STOPPED\r\n")];
            await stream.ReadExactlyAsync(stopped);
            Assert.Equal("SENSUS,1,STOPPED\r\n", Encoding.UTF8.GetString(stopped));

            await StartScannerAsync(stream);
            var firstSample = new byte[Encoding.UTF8.GetByteCount("0,0,0,0,1000,0\r\n")];
            await stream.ReadExactlyAsync(firstSample);
            Assert.Equal("0,0,0,0,1000,0\r\n", Encoding.UTF8.GetString(firstSample));
        }

        [Fact]
        public async Task WriteAsync_InterruptsPendingSample_When_StopCommandArrives()
        {
            // Arrange.
            await using var stream = new ScannerSimulationStream(
                SlowSamples,
                HandshakeResponse
            );
            await StartScannerAsync(stream);
            await using var writer = new StreamWriter(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            var buffer = new byte[Encoding.UTF8.GetByteCount("SENSUS,1,STOPPED\r\n")];

            // Act.
            var pendingRead = stream.ReadAsync(buffer).AsTask();
            await writer.WriteLineAsync("SENSUS,1,STOP");

            // Assert.
            Assert.Equal(buffer.Length, await pendingRead.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal("SENSUS,1,STOPPED\r\n", Encoding.UTF8.GetString(buffer));
        }

        [Fact]
        public async Task WriteAsync_InterruptsPendingSample_When_PrepareCommandArrives()
        {
            // Arrange.
            await using var stream = new ScannerSimulationStream(
                SlowSamples,
                HandshakeResponse
            );
            await StartScannerAsync(stream);
            await using var writer = new StreamWriter(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            var buffer = new byte[Encoding.UTF8.GetByteCount(HandshakeResponse)];

            // Act.
            var pendingRead = stream.ReadAsync(buffer).AsTask();
            await writer.WriteLineAsync("SENSUS,1,PREPARE");

            // Assert.
            Assert.Equal(buffer.Length, await pendingRead.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(HandshakeResponse, Encoding.UTF8.GetString(buffer));
        }

        [Fact]
        public async Task WriteAsync_IgnoresCommands_When_PrefixIsMissingOrRevisionIsWrong()
        {
            // Arrange.
            await using var stream = CreateStream(1000, 2000);
            await using var writer = new StreamWriter(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            await writer.WriteLineAsync("SENSUS,1,PREPARE");
            var response = new byte[Encoding.UTF8.GetByteCount(HandshakeResponse)];
            await stream.ReadExactlyAsync(response);

            // Act & Assert.
            await writer.WriteLineAsync("START");
            await writer.WriteLineAsync("SENSUS,2,START");
            Assert.Equal(0, await stream.ReadAsync(new byte[1]));

            await writer.WriteLineAsync("SENSUS,1,START");
            var firstSample = new byte[Encoding.UTF8.GetByteCount("0,0,0,0,1000,0\r\n")];
            await stream.ReadExactlyAsync(firstSample);
            Assert.Equal("0,0,0,0,1000,0\r\n", Encoding.UTF8.GetString(firstSample));

            await writer.WriteLineAsync("STOP");
            var secondSample = new byte[Encoding.UTF8.GetByteCount("0,0,0,0,2000,0\r\n")];
            await stream.ReadExactlyAsync(secondSample);
            Assert.Equal("0,0,0,0,2000,0\r\n", Encoding.UTF8.GetString(secondSample));
        }
    }
}
