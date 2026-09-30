using Sensus.Models;

namespace Sensus.ViewModels.Factories
{
    public interface IScanViewModelFactory
    {
        ScanViewModel Create(AcquisitionState state);
    }
}
