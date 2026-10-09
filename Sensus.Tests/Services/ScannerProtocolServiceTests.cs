using System.Text;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Serializers;
using Sensus.Services;

namespace Sensus.Tests.Services
{
    public class ScannerProtocolServiceTests
    {
        private const string HandshakeResponse =
            "SENSUS,1,DESCRIPTION\r\n" +
            "SCANNER,Sensus Rover,Mark 1-A\r\n" +
            "BOARD,ELEGOO UNO R3,ATmega328\r\n" +
            "RANGE_SENSOR,HC-SR04,2,400,15\r\n" +
            "SERVO,SG90,180\r\n" +
            "CONFIGURATION,-90,90,1,100,30000\r\n" +
            "SENSUS,1,READY\r\n";

        private static StreamWriter CreateCrLfWriter(Stream stream)
        {
            return new(stream, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
        }

        [Fact]
        public async Task StartScannerAsync_WritesRevisionPrefixedStartLine_When_UsingCrLfWriter()
        {
            // Arrange.
            using var output = new MemoryStream();
            await using var writer = CreateCrLfWriter(output);
            var service = new ScannerProtocolService();

            // Act.
            await service.StartScannerAsync(writer, CancellationToken.None);

            // Assert.
            Assert.Equal("SENSUS,1,START\r\n", Encoding.UTF8.GetString(output.ToArray()));
        }

        [Fact]
        public async Task StopScannerAsync_WritesRevisionPrefixedStopLine_When_UsingCrLfWriter()
        {
            // Arrange.
            using var output = new MemoryStream();
            await using var writer = CreateCrLfWriter(output);
            var service = new ScannerProtocolService();

            // Act.
            await service.StopScannerAsync(writer, CancellationToken.None);

            // Assert.
            Assert.Equal("SENSUS,1,STOP\r\n", Encoding.UTF8.GetString(output.ToArray()));
        }

        [Fact]
        public async Task PerformHandshakeAsync_ReturnsMetadata_When_CompleteResponseFollowsStaleInput()
        {
            // Arrange.
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("stale\r\n" + HandshakeResponse));
            using var output = new MemoryStream();
            using var reader = new StreamReader(input);
            await using var writer = CreateCrLfWriter(output);
            var service = new ScannerProtocolService();

            // Act.
            var handshake = await service.PerformHandshakeAsync(reader, writer, CancellationToken.None);

            // Assert.
            Assert.Equal("Sensus Rover", handshake.Definition.Name);
            Assert.Equal("Mark 1-A", handshake.Definition.Mark);
            Assert.Equal("ATmega328", handshake.Definition.MicrocontrollerBoard.Microcontroller);
            Assert.Equal(400, handshake.Definition.RangeSensor.MaxRangeCm);
            Assert.Equal(new ScannerConfiguration(-90, 90, 1, 100, 30_000), handshake.Configuration);
            Assert.Equal("SENSUS,1,PREPARE\r\n", Encoding.UTF8.GetString(output.ToArray()));
        }

        [Theory]
        [MemberData(nameof(InvalidResponses))]
        public async Task PerformHandshakeAsync_ThrowsInvalidDataException_When_ResponseIsInvalid(string response)
        {
            // Arrange.
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(response));
            using var output = new MemoryStream();
            using var reader = new StreamReader(input);
            await using var writer = CreateCrLfWriter(output);
            var service = new ScannerProtocolService();

            // Act & Assert.
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.PerformHandshakeAsync(reader, writer, CancellationToken.None)
            );
            Assert.Equal("SENSUS,1,PREPARE\r\n", Encoding.UTF8.GetString(output.ToArray()));
        }

        public static IEnumerable<object[]> InvalidResponses()
        {
            yield return [HandshakeResponse.Replace("SENSUS,1,DESCRIPTION", "SENSUS,2,DESCRIPTION")];
            yield return ["SENSUS,1,DESCRIPTION\r\nSENSUS,1,READY\r\n"];
            yield return [HandshakeResponse.Replace("SENSUS,1,READY\r\n", "WRONG\r\n")];
        }

        [Fact]
        public async Task PerformHandshakeAsync_ThrowsEndOfStreamException_When_DisconnectedBeforeReady()
        {
            // Arrange.
            using var input = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    HandshakeResponse.Replace("SENSUS,1,READY\r\n", string.Empty)
                )
            );
            using var output = new MemoryStream();
            using var reader = new StreamReader(input);
            await using var writer = CreateCrLfWriter(output);
            var service = new ScannerProtocolService();

            // Act & Assert.
            await Assert.ThrowsAsync<EndOfStreamException>(() =>
                service.PerformHandshakeAsync(reader, writer, CancellationToken.None)
            );
        }

        [Fact]
        public async Task PerformHandshakeAsync_ReturnsSimulationConfiguration_When_SimulationResponds()
        {
            // Arrange.
            var configuration = new ScannerConfiguration(-45, 45, 15, 1, 30_000);
            var simulation = new ScannerSimulationService();
            await using var stream = simulation.Create(SimulationScenario.ConstantSweep, configuration);
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = CreateCrLfWriter(stream);
            var protocol = new ScannerProtocolService();

            // Act.
            var handshake = await protocol.PerformHandshakeAsync(reader, writer, CancellationToken.None);

            // Assert.
            Assert.Equal("Sensus Rover Simulation", handshake.Definition.Name);
            Assert.Equal("Mark 1-A", handshake.Definition.Mark);
            Assert.Equal(configuration, handshake.Configuration);

            // Act.
            await protocol.StartScannerAsync(writer, CancellationToken.None);
            var sampleLine = await reader.ReadLineAsync(CancellationToken.None);

            // Assert.
            Assert.True(RangeSampleSerializer.TryDeserialize(sampleLine!, out var sample));
            Assert.Equal(configuration.MinBearingDegrees, sample.BearingDegrees);
        }
    }
}
