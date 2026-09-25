using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Services
{
    public class RangeObservationService : IRangeObservationService
    {
        public RangeObservation CreateObservation(RangeSample sample, ScannerDefinition definition)
        {
            var distanceCm = CalculateDistanceCm(sample.RoundTripDurationUs);
            var rangeStatus = GetRangeStatus(definition, distanceCm);

            return new(sample, distanceCm, rangeStatus);
        }

        private double CalculateDistanceCm(uint roundTripDurationUs)
        {
            // Convert round trip pulse duration in microseconds to distance in cm.
            return roundTripDurationUs * 0.034 / 2;
        }

        private RangeStatus GetRangeStatus(ScannerDefinition definition, double distanceCm)
        {
            if (distanceCm > definition.RangeSensor.MaxRangeCm)
            {
                return RangeStatus.TooFar;
            }
            else if (distanceCm < definition.RangeSensor.MinRangeCm)
            {
                return RangeStatus.TooClose;
            }
            else
            {
                return RangeStatus.InRange;
            }
        }
    }
}
