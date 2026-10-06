using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Tests.Models
{
    public class RangeObservationTests
    {
        private static readonly ScannerDefinition Definition = new(
            "Scanner",
            "Test",
            new("Board", "Microcontroller"),
            new("Sensor", 2, 400, 15),
            new("Servo", 180)
        );

        [Theory]
        [InlineData(SampleStatus.NoEcho, 0u)]
        [InlineData(SampleStatus.NoEcho, 1_000u)]
        [InlineData(SampleStatus.Valid, 0u)]
        public void FromSample_LeavesDerivedRangeAbsent_When_SampleHasNoUsableEcho(
            SampleStatus status,
            uint roundTripDurationUs
        )
        {
            // Arrange.
            var sample = new RangeSample(1, 1, 0, 0, roundTripDurationUs, status);

            // Act.
            var observation = RangeObservation.FromSample(sample, Definition);

            // Assert.
            Assert.Null(observation.DistanceCm);
            Assert.Null(observation.RangeStatus);
            Assert.Null(observation.PositionXCm);
            Assert.Null(observation.PositionYCm);
        }

        [Fact]
        public void FromSample_DerivesRange_When_ValidSampleHasNonzeroDuration()
        {
            // Arrange.
            var sample = new RangeSample(1, 1, 0, 0, 1_000, SampleStatus.Valid);

            // Act.
            var observation = RangeObservation.FromSample(sample, Definition);

            // Assert.
            Assert.Equal(17, observation.DistanceCm);
            Assert.Equal(RangeStatus.InRange, observation.RangeStatus);
            Assert.Equal(0, observation.PositionXCm);
            Assert.Equal(17, observation.PositionYCm);
        }
    }
}
