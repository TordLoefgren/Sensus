namespace Sensus.Models
{
    public class ScannerSession
    {
        private readonly List<RangeObservation> _observations = [];

        public IReadOnlyList<RangeObservation> Observations => _observations;

        public RangeObservation? LatestObservation => _observations.Count > 0
            ? _observations[^1]
            : null;

        public void AddObservation(RangeObservation observation)
        {
            _observations.Add(observation);
        }

        public void Clear() => _observations.Clear();
    }
}
