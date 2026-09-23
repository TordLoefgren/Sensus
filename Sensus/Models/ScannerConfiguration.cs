namespace Sensus.Models
{
    public readonly record struct ScannerConfiguration(
        double MinBearingDegrees,
        double MaxBearingDegrees,
        double BearingStepDegrees,
        uint AcquisitionDelayMs,
        uint EchoTimeoutUs
    );
}
