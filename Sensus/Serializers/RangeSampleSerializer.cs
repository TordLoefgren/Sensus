using System.Globalization;
using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Serializers
{
    public static class RangeSampleSerializer
    {
        public static string Serialize(RangeSample rangeSample)
        {
            return string.Concat(
                rangeSample.Sequence.ToString(CultureInfo.InvariantCulture),
                ",",
                rangeSample.SweepId.ToString(CultureInfo.InvariantCulture),
                ",",
                rangeSample.ElapsedUs.ToString(CultureInfo.InvariantCulture),
                ",",
                rangeSample.BearingDegrees.ToString(CultureInfo.InvariantCulture),
                ",",
                rangeSample.RoundTripDurationUs.ToString(CultureInfo.InvariantCulture),
                ",",
                ((byte)rangeSample.Status).ToString(CultureInfo.InvariantCulture),
                "\r\n"
            );
        }

        public static bool TryDeserialize(string value, out RangeSample outValue)
        {
            outValue = default;
            bool success = true;

            var values = value.Split(',');

            if (values.Length != 6)
            {
                return false;
            }

            success &= uint.TryParse(values[0], CultureInfo.InvariantCulture, out var sequence);
            success &= uint.TryParse(values[1], CultureInfo.InvariantCulture, out var sweepId);
            success &= uint.TryParse(values[2], CultureInfo.InvariantCulture, out var elapsedUs);
            success &= double.TryParse(values[3], CultureInfo.InvariantCulture, out var bearingDegrees);
            success &= uint.TryParse(values[4], CultureInfo.InvariantCulture, out var roundTripDurationUs);

            if (!byte.TryParse(values[5], CultureInfo.InvariantCulture, out var statusValue))
            {
                return false;
            }

            if (!Enum.IsDefined((SampleStatus)statusValue))
            {
                return false;
            }

            var status = (SampleStatus)statusValue;

            if (success)
            {
                outValue = new(
                    sequence,
                    sweepId,
                    elapsedUs,
                    bearingDegrees,
                    roundTripDurationUs,
                    status
                );
            }

            return success;
        }
    }
}
