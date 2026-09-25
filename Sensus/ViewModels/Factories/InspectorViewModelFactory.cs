using Sensus.Models;

namespace Sensus.ViewModels.Factories
{
    public class InspectorViewModelFactory : IInspectorViewModelFactory
    {
        public InspectorViewModel Create(AcquisitionState state)
        {
            return new(state);
        }
    }
}
