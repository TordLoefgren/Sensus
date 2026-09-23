using Sensus.Enums;

namespace Sensus.Models
{
    public readonly record struct RangeSample(
        uint Sequence,
        uint SweepId,
        uint ElapsedUs,
        double BearingDegrees,
        uint RoundTripDurationUs,
        SampleStatus Status
    );
}
