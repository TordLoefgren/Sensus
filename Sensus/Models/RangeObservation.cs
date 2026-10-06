using Sensus.Models.Enums;

namespace Sensus.Models
{
    public readonly record struct RangeObservation(
        RangeSample Sample,
        double? DistanceCm,
        RangeStatus? RangeStatus
    )
    {
        public static RangeObservation FromSample(RangeSample sample, ScannerDefinition definition)
        {
            if (sample.Status != SampleStatus.Valid || sample.RoundTripDurationUs == 0)
            {
                return new(sample, null, null);
            }

            var distanceCm = CalculateDistanceCm(sample.RoundTripDurationUs);
            var rangeStatus = GetRangeStatus(definition, distanceCm);

            return new(sample, distanceCm, rangeStatus);
        }

        public double BearingRadians => double.DegreesToRadians(Sample.BearingDegrees);

        public double ElapsedSeconds => Sample.ElapsedUs / 1_000_000.0;

        public double? PositionXCm => Math.Sin(BearingRadians) * DistanceCm;

        public double? PositionYCm => Math.Cos(BearingRadians) * DistanceCm;

        private static double CalculateDistanceCm(uint roundTripDurationUs)
        {
            // Convert round trip pulse duration in microseconds to distance in cm.
            return roundTripDurationUs * 0.034 / 2;
        }

        private static RangeStatus GetRangeStatus(ScannerDefinition definition, double distanceCm)
        {
            if (distanceCm > definition.RangeSensor.MaxRangeCm)
            {
                return Enums.RangeStatus.TooFar;
            }
            else if (distanceCm < definition.RangeSensor.MinRangeCm)
            {
                return Enums.RangeStatus.TooClose;
            }
            else
            {
                return Enums.RangeStatus.InRange;
            }
        }
    }
}
