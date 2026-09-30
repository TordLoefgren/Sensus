using Sensus.Models;

namespace Sensus.ViewModels.Factories
{
    public class ScanViewModelFactory : IScanViewModelFactory
    {
        public ScanViewModel Create(AcquisitionState state)
        {
            return new(state);
        }
    }
}
