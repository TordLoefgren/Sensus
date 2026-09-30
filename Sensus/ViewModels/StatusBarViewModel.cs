using System.ComponentModel;
using Sensus.Extensions;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Services;

namespace Sensus.ViewModels
{
    public class StatusBarViewModel : ObservableObject, IDisposable
    {
        private readonly ScanViewModel _scan;
        private readonly ISerialConnectionService _serialConnectionService;

        public AcquisitionState State { get; }

        public string SourceStatusDisplay => State.SourceState == SourceState.Idle
            ? State.SourceState.ToDisplayString()
            : $"{SourceIdentityDisplay} · {State.SourceState.ToDisplayString()}";

        private string SourceIdentityDisplay => State.SourceType switch
        {
            SourceType.Serial => $"Serial · {_serialConnectionService.PortName}",
            SourceType.Simulation => "Simulation",
            _ => "Source"
        };

        private string _mousePositionDisplay = "Undefined";
        public string MousePositionDisplay
        {
            get => _mousePositionDisplay;
            private set => SetField(ref _mousePositionDisplay, value);
        }

        public StatusBarViewModel(
            AcquisitionState state,
            ScanViewModel scan,
            ISerialConnectionService serialConnectionService
        )
        {
            State = state;

            _scan = scan;
            _serialConnectionService = serialConnectionService;

            UpdateMousePosition();

            PropertyChangedEventManager.AddHandler(_scan, OnMousePositionChanged, nameof(ScanViewModel.MousePositionCm));
            PropertyChangedEventManager.AddHandler(State, OnSourceStatusChanged, nameof(AcquisitionState.SourceState));
            PropertyChangedEventManager.AddHandler(State, OnSourceStatusChanged, nameof(AcquisitionState.SourceType));
        }

        public void Dispose()
        {
            PropertyChangedEventManager.RemoveHandler(State, OnSourceStatusChanged, nameof(AcquisitionState.SourceState));
            PropertyChangedEventManager.RemoveHandler(State, OnSourceStatusChanged, nameof(AcquisitionState.SourceType));
            PropertyChangedEventManager.RemoveHandler(_scan, OnMousePositionChanged, nameof(ScanViewModel.MousePositionCm));
        }

        private void OnSourceStatusChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SourceStatusDisplay));
        }

        private void OnMousePositionChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateMousePosition();
        }

        private void UpdateMousePosition()
        {
            if (_scan.MousePositionCm is not { } point)
            {
                MousePositionDisplay = "Undefined";

                return;
            }

            MousePositionDisplay = $"{(int)point.X} × {(int)point.Y} cm";
        }
    }
}
