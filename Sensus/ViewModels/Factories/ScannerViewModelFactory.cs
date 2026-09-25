using Sensus.Models;
using Sensus.Services;

namespace Sensus.ViewModels.Factories
{
    public class ScannerViewModelFactory : IScannerViewModelFactory
    {
        public ScannerViewModel Create(
            AcquisitionState state,
            ISerialConnectionService serialConnectionService,
            IAcquisitionService acquisitionService
        )
        {
            return new(state, serialConnectionService, acquisitionService);
        }
    }
}
