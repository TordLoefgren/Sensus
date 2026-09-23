using Sensus.Models;

namespace Sensus.Services
{
    public interface IRangeSampleSerializerService
    {
        string Serialize(RangeSample value);
        bool TryDeserialize(string value, out RangeSample outValue);
    }
}
