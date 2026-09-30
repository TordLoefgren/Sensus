using System.Windows;
using Sensus.Models;

namespace Sensus.ViewModels
{
    public class ScanViewModel : ObservableObject
    {
        public AcquisitionState State { get; }

        private Point? _mousePositionCm;
        public Point? MousePositionCm
        {
            get => _mousePositionCm;
            set => SetField(ref _mousePositionCm, value);
        }

        public ScanViewModel(AcquisitionState state)
        {
            State = state;
        }

        public void UpdateMousePosition(Point? positionCm) => MousePositionCm = positionCm;
    }
}
