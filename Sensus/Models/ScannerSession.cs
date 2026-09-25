using System.Collections.ObjectModel;

namespace Sensus.Models
{
    public class ScannerSession : ObservableObject
    {
        public ScannerDefinition Definition { get; }
        public ScannerConfiguration Configuration { get; }

        private readonly ObservableCollection<RangeObservation> _observations = [];
        public ReadOnlyObservableCollection<RangeObservation> Observations { get; }

        public RangeObservation? LatestObservation => _observations.Count > 0
            ? _observations[^1]
            : null;

        public ScannerSession(ScannerDefinition definition, ScannerConfiguration configuration)
        {
            Definition = definition;
            Configuration = configuration;
            Observations = new(_observations);
        }

        public void AddObservation(RangeObservation observation)
        {
            _observations.Add(observation);

            OnPropertyChanged(nameof(LatestObservation));
        }

        public void Clear()
        {
            _observations.Clear();

            OnPropertyChanged(nameof(LatestObservation));
        }
    }
}
