using Sensus.Models;

namespace Sensus.Services
{
    public interface IRangeObservationService
    {
        public RangeObservation CreateObservation(RangeSample sample, ScannerDefinition definition);
    }
}
