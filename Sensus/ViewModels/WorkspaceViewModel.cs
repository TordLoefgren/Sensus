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
        public ViewportViewModel Viewport { get; }
        public InspectorViewModel Inspector { get; }
        public StatusBarViewModel StatusBar { get; }

        public WorkspaceViewModel(
            AcquisitionState state,
            IAcquisitionService acquisitionService,
            ISerialConnectionService serialConnectionService,
            IScannerViewModelFactory scannerViewModelFactory,
            IViewportViewModelFactory viewportViewModelFactory,
            IInspectorViewModelFactory inspectorViewModelFactory,
            IStatusBarViewModelFactory statusBarViewModelFactory
        )
        {
            State = state;
            _acquisitionService = acquisitionService;
            _serialConnectionService = serialConnectionService;

            Scanner = scannerViewModelFactory.Create(State, serialConnectionService, acquisitionService);
            Viewport = viewportViewModelFactory.Create(State);
            Inspector = inspectorViewModelFactory.Create(State);
            StatusBar = statusBarViewModelFactory.Create(State, Viewport, serialConnectionService);
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
