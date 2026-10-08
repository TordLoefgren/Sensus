namespace Sensus.Models
{
    public readonly record struct ScannerConfiguration(
        double MinBearingDegrees,
        double MaxBearingDegrees,
        double BearingStepDegrees,
        uint AcquisitionDelayMs,
        uint EchoTimeoutUs
    )
    {
        public static ScannerConfiguration Default => new(-80.0, 80.0, 5.0, 60, 30_000);
    }
}
