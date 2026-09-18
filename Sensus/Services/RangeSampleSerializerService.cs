using System.Globalization;

namespace Sensus.Services
{
    public class RangeSampleSerializerService : IRangeSampleSerializerService
    {
        public string Serialize(RangeSample rangeSample)
        {
            return $"{rangeSample.RoundTripDurationUs.ToString(CultureInfo.InvariantCulture)}\r\n";
        }

        public bool TryDeserialize(string value, out RangeSample outValue)
        {
            if (uint.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
            {
                outValue = new(parsed);
                return true;
            }

            outValue = default;
            return false;
        }
    }
}
