using Sensus.Models;
using Sensus.Services;

namespace Sensus.ViewModels.Factories
{
    public interface IScannerViewModelFactory
    {
        ScannerViewModel Create(AcquisitionState state, ISerialConnectionService serialConnectionService, IAcquisitionService acquisitionService);
    }
}
