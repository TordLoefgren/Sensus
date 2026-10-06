using Sensus.Serializers;

namespace Sensus.Tests.Serializers
{
    public class RangeSampleSerializerTests
    {
        [Fact]
        public void TryDeserialize_ReturnsFalse_When_RoundTripDurationIsNegative()
        {
            // Arrange.
            const string line = "1,1,1000,0,-1,0";

            // Act.
            var success = RangeSampleSerializer.TryDeserialize(line, out var sample);

            // Assert.
            Assert.False(success);
            Assert.Equal(default, sample);
        }
    }
}
