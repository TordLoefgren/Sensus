using Sensus.Enums;

namespace Sensus.Models
{
    public readonly record struct RangeObservation(
        RangeSample Sample,
        double DistanceCm,
        RangeStatus RangeStatus
    )
    {
        public double BearingRadians => double.DegreesToRadians(Sample.BearingDegrees);

        public double ElapsedSeconds => Sample.ElapsedUs / 1_000_000.0;

        public double PositionXCm => Math.Sin(BearingRadians) * DistanceCm;

        public double PositionYCm => Math.Cos(BearingRadians) * DistanceCm;
    }
}
