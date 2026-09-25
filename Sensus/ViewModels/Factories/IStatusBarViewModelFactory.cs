using Sensus.Models;
using Sensus.Services;

namespace Sensus.ViewModels.Factories
{
    public interface IStatusBarViewModelFactory
    {
        StatusBarViewModel Create(AcquisitionState state, ViewportViewModel viewport, ISerialConnectionService serialConnectionService);
    }
}
