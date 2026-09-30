using Sensus.Models;
using Sensus.Services;
using Sensus.ViewModels.Factories;

namespace Sensus.ViewModels
{
    public class WorkspaceViewModel : ObservableObject
    {
        private readonly IAcquisitionService _acquisitionService;
        private readonly ISerialConnectionService _serialConnectionService;

        public AcquisitionState State { get; }
        public ScannerViewModel Scanner { get; }
        public ScanViewModel Scan { get; }
        public InspectorViewModel Inspector { get; }
        public StatusBarViewModel StatusBar { get; }

        public WorkspaceViewModel(
            AcquisitionState state,
            IAcquisitionService acquisitionService,
            ISerialConnectionService serialConnectionService,
            IScannerViewModelFactory scannerViewModelFactory,
            IScanViewModelFactory scanViewModelFactory,
            IInspectorViewModelFactory inspectorViewModelFactory,
            IStatusBarViewModelFactory statusBarViewModelFactory
        )
        {
            State = state;
            _acquisitionService = acquisitionService;
            _serialConnectionService = serialConnectionService;

            Scanner = scannerViewModelFactory.Create(State, serialConnectionService, acquisitionService);
            Scan = scanViewModelFactory.Create(State);
            Inspector = inspectorViewModelFactory.Create(State);
            StatusBar = statusBarViewModelFactory.Create(State, Scan, serialConnectionService);
        }

        public async Task ShutdownAsync()
        {
            try
            {
                await _acquisitionService.StopAsync();
            }
            finally
            {
                _serialConnectionService.Dispose();
                Scanner.Dispose();
                Inspector.Dispose();
                StatusBar.Dispose();
            }
        }
    }
}
