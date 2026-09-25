using Sensus.Models;
using Sensus.Services;

namespace Sensus.ViewModels.Factories
{
    public class StatusBarViewModelFactory : IStatusBarViewModelFactory
    {
        public StatusBarViewModel Create(AcquisitionState state, ViewportViewModel viewport, ISerialConnectionService serialConnectionService)
        {
            return new(state, viewport, serialConnectionService);
        }
    }
}
