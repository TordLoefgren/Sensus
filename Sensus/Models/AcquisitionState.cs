using Sensus.Models.Enums;

namespace Sensus.Models
{
    public class AcquisitionState : ObservableObject
    {
        private ScannerSession? _session;
        public ScannerSession? Session
        {
            get => _session;
            set => SetField(ref _session, value);
        }

        private SourceState _sourceState = SourceState.Idle;
        public SourceState SourceState
        {
            get => _sourceState;
            set => SetField(ref _sourceState, value);
        }

        private SourceType _sourceType = SourceType.None;
        public SourceType SourceType
        {
            get => _sourceType;
            set => SetField(ref _sourceType, value);
        }
    }
}
