using Sensus.Models;
using Sensus.Services;

namespace Sensus.ViewModels.Factories
{
    public class StatusBarViewModelFactory : IStatusBarViewModelFactory
    {
        public StatusBarViewModel Create(AcquisitionState state, ScanViewModel scan, ISerialConnectionService serialConnectionService)
        {
            return new(state, scan, serialConnectionService);
        }
    }
}
