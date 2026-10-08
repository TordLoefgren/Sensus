using System.Globalization;
using Sensus.Models;
using Sensus.Serializers;

namespace Sensus.Tests.Serializers
{
    public class ScannerHandshakeResponseSerializerTests
    {
        private const string Response =
            "SENSUS,1,DESCRIPTION\r\n" +
            "SCANNER,Sensus Rover,Mark 1-A\r\n" +
            "BOARD,ELEGOO UNO R3,ATmega328\r\n" +
            "RANGE_SENSOR,HC-SR04,2,400,15\r\n" +
            "SERVO,SG90,180\r\n" +
            "CONFIGURATION,-90,90,1,100,30000\r\n" +
            "SENSUS,1,READY\r\n";

        private static ScannerHandshakeResponse CreateHandshake()
        {
            return new(
                new(
                    "Sensus Rover",
                    "Mark 1-A",
                    new("ELEGOO UNO R3", "ATmega328"),
                    new("HC-SR04", 2, 400, 15),
                    new("SG90", 180)
                ),
                new(-90, 90, 1, 100, 30_000)
            );
        }

        [Fact]
        public void Serialize_WritesProtocolResponse_When_HandshakeIsValid()
        {
            // Arrange.
            var handshake = CreateHandshake();

            // Act.
            var response = ScannerHandshakeResponseSerializer.Serialize(handshake);

            // Assert.
            Assert.Equal(Response, response);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TryDeserialize_ReturnsHandshake_When_FinalCrLfIsPresentOrAbsent(bool includeFinalCrLf)
        {
            // Arrange.
            var response = includeFinalCrLf ? Response : Response[..^2];

            // Act.
            var success = ScannerHandshakeResponseSerializer.TryDeserialize(response, out var result);

            // Assert.
            Assert.True(success);
            Assert.Equal(CreateHandshake(), result);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("da-DK")]
        public void SerializeAndTryDeserialize_UseInvariantCulture_When_CurrentCultureVaries(string cultureName)
        {
            // Arrange.
            var originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var handshake = CreateHandshake();
                handshake = handshake with
                {
                    Definition = handshake.Definition with
                    {
                        RangeSensor = new("HC-SR04", 2.5, 400.25, 15.125),
                        ServoMotor = new("SG90", 180.5)
                    },
                    Configuration = new(-89.5, 89.25, 0.125, 0, uint.MaxValue)
                };

                var expected = Response
                    .Replace("2,400,15", "2.5,400.25,15.125")
                    .Replace("SG90,180", "SG90,180.5")
                    .Replace("-90,90,1,100,30000", "-89.5,89.25,0.125,0,4294967295");

                // Act.
                var serialized = ScannerHandshakeResponseSerializer.Serialize(handshake);
                var success = ScannerHandshakeResponseSerializer.TryDeserialize(expected, out var result);

                // Assert.
                Assert.Equal(expected, serialized);
                Assert.True(success);
                Assert.Equal(handshake, result);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Sensus,Rover")]
        [InlineData("Sensus\rRover")]
        [InlineData("Sensus\nRover")]
        public void Serialize_ThrowsArgumentException_When_ScannerNameIsInvalid(string? name)
        {
            // Arrange.
            var handshake = CreateHandshake();
            handshake = handshake with
            {
                Definition = handshake.Definition with { Name = name! }
            };

            // Act & Assert.
            Assert.Throws<ArgumentException>(() => ScannerHandshakeResponseSerializer.Serialize(handshake));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Serialize_ThrowsArgumentException_When_BearingStepIsNonFinite(double bearingStepDegrees)
        {
            // Arrange.
            var handshake = CreateHandshake();
            handshake = handshake with
            {
                Configuration = handshake.Configuration with
                {
                    BearingStepDegrees = bearingStepDegrees
                }
            };

            // Act & Assert.
            Assert.Throws<ArgumentException>(() => ScannerHandshakeResponseSerializer.Serialize(handshake));
        }

        [Theory]
        [MemberData(nameof(MalformedResponses))]
        public void TryDeserialize_ReturnsFalse_When_ResponseIsMalformed(string? response)
        {
            // Arrange & Act.
            var success = ScannerHandshakeResponseSerializer.TryDeserialize(response!, out var result);

            // Assert.
            Assert.False(success);
            Assert.Equal(default(ScannerHandshakeResponse), result);
        }

        public static IEnumerable<object?[]> MalformedResponses()
        {
            yield return [null];
            yield return [string.Empty];
            yield return [Response.Replace("SENSUS,1,DESCRIPTION", "SENSUS,2,DESCRIPTION")];
            yield return [Response.Replace("SENSUS,1,READY\r\n", string.Empty)];
            yield return [Response.Replace("SENSUS,1,READY", "SENSUS,1,START")];
            yield return [Response.Replace("BOARD,ELEGOO UNO R3,ATmega328\r\n", string.Empty)];
            yield return [Response.Replace("BOARD,ELEGOO UNO R3,ATmega328", "SCANNER,Sensus Rover,Mark 1-A")];
            yield return [Response.Replace("SCANNER,Sensus Rover,Mark 1-A\r\nBOARD,ELEGOO UNO R3,ATmega328", "BOARD,ELEGOO UNO R3,ATmega328\r\nSCANNER,Sensus Rover,Mark 1-A")];
            yield return [Response.Replace("SERVO,SG90,180", "SERVO,SG90")];
            yield return [Response.Replace("SERVO,SG90,180", "SERVO,SG90,180,extra")];
            yield return [Response.Replace("Sensus Rover", " ")];
            yield return [Response.Replace("Sensus Rover", "Sensus\nRover")];
            yield return [Response.Replace("Sensus Rover", "Sensus\rRover")];
            yield return [Response.Replace("ATmega328", string.Empty)];
            yield return [Response.Replace("2,400,15", "invalid,400,15")];
            yield return [Response.Replace("2,400,15", "2,NaN,15")];
            yield return [Response.Replace("2,400,15", "2,400,Infinity")];
            yield return [Response.Replace("SG90,180", "SG90,-Infinity")];
            yield return [Response.Replace("-90,90,1,100,30000", "-90,1e999,1,100,30000")];
            yield return [Response.Replace("-90,90,1,100,30000", "-90,90,1,-1,30000")];
            yield return [Response.Replace("-90,90,1,100,30000", "-90,90,1,1.5,30000")];
            yield return [Response.Replace("-90,90,1,100,30000", "-90,90,1,100,4294967296")];
            yield return [Response.Replace("\r\n", "\n")];
            yield return [Response.Replace("SENSUS,1,READY", "\r\nSENSUS,1,READY")];
            yield return [Response + "\r\n"];
            yield return [Response + "extra\r\n"];
            yield return [Response + Response];
        }
    }
}
