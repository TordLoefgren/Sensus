using System.ComponentModel;
using Sensus.Extensions;
using Sensus.Models;

namespace Sensus.ViewModels
{
    public class InspectorViewModel : ObservableObject, IDisposable
    {

        #region State

        public AcquisitionState State { get; }

        private ScannerSession? _session;

        #endregion

        #region Sample Display

        private string _sequenceDisplay = "-";
        public string SequenceDisplay
        {
            get => _sequenceDisplay;
            private set => SetField(ref _sequenceDisplay, value);
        }

        private string _sweepIdDisplay = "-";
        public string SweepIdDisplay
        {
            get => _sweepIdDisplay;
            private set => SetField(ref _sweepIdDisplay, value);
        }

        private string _elapsedDisplay = "-";
        public string ElapsedDisplay
        {
            get => _elapsedDisplay;
            private set => SetField(ref _elapsedDisplay, value);
        }

        private string _bearingDisplay = "-";
        public string BearingDisplay
        {
            get => _bearingDisplay;
            private set => SetField(ref _bearingDisplay, value);
        }

        private string _roundTripDisplay = "-";
        public string RoundTripDisplay
        {
            get => _roundTripDisplay;
            private set => SetField(ref _roundTripDisplay, value);
        }

        private string _statusDisplay = "-";
        public string StatusDisplay
        {
            get => _statusDisplay;
            private set => SetField(ref _statusDisplay, value);
        }

        #endregion

        #region Observation Display

        private string _elapsedSecondsDisplay = "-";
        public string ElapsedSecondsDisplay
        {
            get => _elapsedSecondsDisplay;
            private set => SetField(ref _elapsedSecondsDisplay, value);
        }

        private string _distanceDisplay = "-";
        public string DistanceDisplay
        {
            get => _distanceDisplay;
            private set => SetField(ref _distanceDisplay, value);
        }

        private string _positionXDisplay = "-";
        public string PositionXDisplay
        {
            get => _positionXDisplay;
            private set => SetField(ref _positionXDisplay, value);
        }

        private string _positionYDisplay = "-";
        public string PositionYDisplay
        {
            get => _positionYDisplay;
            private set => SetField(ref _positionYDisplay, value);
        }

        private string _rangeStatusDisplay = "-";
        public string RangeStatusDisplay
        {
            get => _rangeStatusDisplay;
            private set => SetField(ref _rangeStatusDisplay, value);
        }

        #endregion

        public InspectorViewModel(AcquisitionState state)
        {
            State = state;

            PropertyChangedEventManager.AddHandler(State, OnSessionChanged, nameof(AcquisitionState.Session));

            UpdateSessionSubscription();
        }

        public void Dispose()
        {
            PropertyChangedEventManager.RemoveHandler(State, OnSessionChanged, nameof(AcquisitionState.Session));

            if (_session is { } session)
            {
                PropertyChangedEventManager.RemoveHandler(session, OnLatestObservationChanged, nameof(ScannerSession.LatestObservation));
            }
        }

        #region Session Tracking

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateSessionSubscription();
        }

        private void UpdateSessionSubscription()
        {
            if (_session is { } previous)
            {
                PropertyChangedEventManager.RemoveHandler(previous, OnLatestObservationChanged, nameof(ScannerSession.LatestObservation));
            }

            _session = State.Session;
            if (_session is { } current)
            {
                PropertyChangedEventManager.AddHandler(current, OnLatestObservationChanged, nameof(ScannerSession.LatestObservation));
            }

            UpdateLatestObservation();
        }

        private void OnLatestObservationChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateLatestObservation();
        }

        private void UpdateLatestObservation()
        {
            var observation = _session?.LatestObservation;
            UpdateSampleDisplay(observation?.Sample);
            UpdateObservationDisplay(observation);
        }

        #endregion

        #region Display Updates

        private void UpdateSampleDisplay(RangeSample? rangeSample)
        {
            if (rangeSample is { } sample)
            {
                SequenceDisplay = $"{sample.Sequence}";
                SweepIdDisplay = $"{sample.SweepId}";
                ElapsedDisplay = $"{sample.ElapsedUs} μs";
                BearingDisplay = $"{sample.BearingDegrees:F2} °";
                RoundTripDisplay = $"{sample.RoundTripDurationUs} μs";
                StatusDisplay = sample.Status.ToDisplayString();
            }
            else
            {
                SequenceDisplay = "-";
                SweepIdDisplay = "-";
                ElapsedDisplay = "-";
                BearingDisplay = "-";
                RoundTripDisplay = "-";
                StatusDisplay = "-";
            }
        }

        private void UpdateObservationDisplay(RangeObservation? rangeObservation)
        {
            if (rangeObservation is { } observation)
            {
                ElapsedSecondsDisplay = $"{observation.ElapsedSeconds:F2} s";
                DistanceDisplay = $"{observation.DistanceCm:F2} cm";
                PositionXDisplay = $"{observation.PositionXCm:F2} cm";
                PositionYDisplay = $"{observation.PositionYCm:F2} cm";
                RangeStatusDisplay = observation.RangeStatus.ToDisplayString();
            }
            else
            {
                ElapsedSecondsDisplay = "-";
                DistanceDisplay = "-";
                PositionXDisplay = "-";
                PositionYDisplay = "-";
                RangeStatusDisplay = "-";
            }
        }

        #endregion

    }
}
