using System.Diagnostics;
using System.Runtime.CompilerServices;
using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Simulation
{
    public static class ScannerSimulationGenerator
    {
        private static uint GetElapsedMicroseconds(Stopwatch stopwatch)
        {
            // Wrap to 32 bits like the device's microsecond counter.
            return unchecked((uint)(ulong)stopwatch.Elapsed.TotalMicroseconds);
        }

        public static async IAsyncEnumerable<RangeSample> ConstantRangeSweep(
            ScannerConfiguration scannerConfiguration,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            uint sequence = 1;
            uint sweepId = 1;
            uint roundTripDurationUs = 14_000;

            double bearingDegrees = scannerConfiguration.MinBearingDegrees;
            bool sweepingClockwise = true;

            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                await Task.Delay((int)scannerConfiguration.AcquisitionDelayMs, cancellationToken);

                var currentSequence = sequence;
                var currentSweepId = sweepId;
                var currentBearingDegrees = bearingDegrees;

                // Update
                sequence++;

                if (sweepingClockwise && bearingDegrees >= scannerConfiguration.MaxBearingDegrees)
                {
                    sweepingClockwise = false;
                    sweepId++;
                }
                else if (!sweepingClockwise && bearingDegrees <= scannerConfiguration.MinBearingDegrees)
                {
                    sweepingClockwise = true;
                    sweepId++;
                }

                bearingDegrees += scannerConfiguration.BearingStepDegrees * (sweepingClockwise ? 1 : -1);

                yield return new(
                    currentSequence,
                    currentSweepId,
                    GetElapsedMicroseconds(stopwatch),
                    currentBearingDegrees,
                    roundTripDurationUs,
                    SampleStatus.Valid
                );
            }
        }

        public static async IAsyncEnumerable<RangeSample> SymmetricRangeSweep(
            ScannerConfiguration scannerConfiguration,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            uint sequence = 1;
            uint sweepId = 1;

            double bearingDegrees = scannerConfiguration.MinBearingDegrees;

            bool sweepingClockwise = true;

            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                await Task.Delay((int)scannerConfiguration.AcquisitionDelayMs, cancellationToken);

                var currentSequence = sequence;
                var currentSweepId = sweepId;
                var currentBearingDegrees = bearingDegrees;

                var bearingRadians = double.DegreesToRadians(currentBearingDegrees);
                var currentRoundTripDurationUs = 12_000u + (uint)(8_000 * (1.0 + Math.Cos(6.0 * bearingRadians)) / 2.0);

                // Update
                sequence++;

                if (sweepingClockwise && bearingDegrees >= scannerConfiguration.MaxBearingDegrees)
                {
                    sweepingClockwise = false;
                    sweepId++;
                }
                else if (!sweepingClockwise && bearingDegrees <= scannerConfiguration.MinBearingDegrees)
                {
                    sweepingClockwise = true;
                    sweepId++;
                }

                bearingDegrees += scannerConfiguration.BearingStepDegrees * (sweepingClockwise ? 1 : -1);

                yield return new(
                    currentSequence,
                    currentSweepId,
                    GetElapsedMicroseconds(stopwatch),
                    currentBearingDegrees,
                    currentRoundTripDurationUs,
                    SampleStatus.Valid
                );
            }
        }

        public static async IAsyncEnumerable<RangeSample> MovingRangeSweep(
            ScannerConfiguration scannerConfiguration,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            uint sequence = 1;
            uint sweepId = 1;
            uint roundTripDurationUs = 10_000;

            double bearingDegrees = scannerConfiguration.MinBearingDegrees;
            bool sweepingClockwise = true;

            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                await Task.Delay((int)scannerConfiguration.AcquisitionDelayMs, cancellationToken);

                var currentSequence = sequence;
                var currentSweepId = sweepId;
                var currentBearingDegrees = bearingDegrees;
                var currentRoundTripDurationUs = roundTripDurationUs;

                // Update
                sequence++;

                if (sweepingClockwise && bearingDegrees >= scannerConfiguration.MaxBearingDegrees)
                {
                    sweepingClockwise = false;
                    sweepId++;
                    roundTripDurationUs += 2_000;
                }
                else if (!sweepingClockwise && bearingDegrees <= scannerConfiguration.MinBearingDegrees)
                {
                    sweepingClockwise = true;
                    sweepId++;
                    roundTripDurationUs += 2_000;
                }

                if (roundTripDurationUs > 20_000)
                {
                    roundTripDurationUs = 10_000;
                }

                bearingDegrees += scannerConfiguration.BearingStepDegrees * (sweepingClockwise ? 1 : -1);

                yield return new(
                    currentSequence,
                    currentSweepId,
                    GetElapsedMicroseconds(stopwatch),
                    currentBearingDegrees,
                    currentRoundTripDurationUs,
                    SampleStatus.Valid
                );
            }
        }

        public static async IAsyncEnumerable<RangeSample> LimitRangeSweep(
            ScannerConfiguration scannerConfiguration,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            uint sequence = 1;
            uint sweepId = 1;
            uint roundTripDurationUs = 22_000;

            double bearingDegrees = scannerConfiguration.MinBearingDegrees;
            bool sweepingClockwise = true;

            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                await Task.Delay((int)scannerConfiguration.AcquisitionDelayMs, cancellationToken);

                var currentSequence = sequence;
                var currentSweepId = sweepId;
                var currentBearingDegrees = bearingDegrees;

                // Update
                sequence++;

                if (sweepingClockwise && bearingDegrees >= scannerConfiguration.MaxBearingDegrees)
                {
                    sweepingClockwise = false;
                    sweepId++;
                }
                else if (!sweepingClockwise && bearingDegrees <= scannerConfiguration.MinBearingDegrees)
                {
                    sweepingClockwise = true;
                    sweepId++;
                }

                roundTripDurationUs += 2_000;
                if (roundTripDurationUs > 26_000)
                {
                    roundTripDurationUs = 22_000;
                }

                bearingDegrees += scannerConfiguration.BearingStepDegrees * (sweepingClockwise ? 1 : -1);

                yield return new(
                    currentSequence,
                    currentSweepId,
                    GetElapsedMicroseconds(stopwatch),
                    currentBearingDegrees,
                    roundTripDurationUs,
                    SampleStatus.Valid
                );
            }
        }

        public static async IAsyncEnumerable<RangeSample> NoEchoRangeSweep(
            ScannerConfiguration scannerConfiguration,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            // Flip the status on return so the end interval does not hold one status twice.
            const int intervalCount = 5;
            uint sequence = 1;
            uint sweepId = 1;

            double bearingDegrees = scannerConfiguration.MinBearingDegrees;
            bool sweepingClockwise = true;
            var intervalDegrees = (scannerConfiguration.MaxBearingDegrees - scannerConfiguration.MinBearingDegrees) / intervalCount;

            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                await Task.Delay((int)scannerConfiguration.AcquisitionDelayMs, cancellationToken);

                var currentSequence = sequence;
                var currentSweepId = sweepId;
                var currentBearingDegrees = bearingDegrees;
                var interval = Math.Clamp(
                    (int)((currentBearingDegrees - scannerConfiguration.MinBearingDegrees) / intervalDegrees),
                    0,
                    intervalCount - 1
                );
                var status = (interval % 2 == 0) == sweepingClockwise
                    ? SampleStatus.Valid
                    : SampleStatus.NoEcho;

                // Update
                sequence++;

                if (sweepingClockwise && bearingDegrees >= scannerConfiguration.MaxBearingDegrees)
                {
                    sweepingClockwise = false;
                    sweepId++;
                }
                else if (!sweepingClockwise && bearingDegrees <= scannerConfiguration.MinBearingDegrees)
                {
                    sweepingClockwise = true;
                    sweepId++;
                }

                bearingDegrees += scannerConfiguration.BearingStepDegrees * (sweepingClockwise ? 1 : -1);

                yield return new(
                    currentSequence,
                    currentSweepId,
                    GetElapsedMicroseconds(stopwatch),
                    currentBearingDegrees,
                    status == SampleStatus.Valid ? 14_000u : 0u,
                    status
                );
            }
        }

        public static Func<CancellationToken, IAsyncEnumerable<RangeSample>> Create(
            SimulationScenario scenario,
            ScannerConfiguration scannerConfiguration
        )
        {
            return scenario switch
            {
                SimulationScenario.ConstantSweep => cancellationToken =>
                    ConstantRangeSweep(scannerConfiguration, cancellationToken),

                SimulationScenario.SymmetricSweep => cancellationToken =>
                    SymmetricRangeSweep(scannerConfiguration, cancellationToken),

                SimulationScenario.MovingSweep => cancellationToken =>
                    MovingRangeSweep(scannerConfiguration, cancellationToken),

                SimulationScenario.LimitSweep => cancellationToken =>
                    LimitRangeSweep(scannerConfiguration, cancellationToken),

                SimulationScenario.NoEchoSweep => cancellationToken =>
                    NoEchoRangeSweep(scannerConfiguration, cancellationToken),

                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };
        }
    }
}
