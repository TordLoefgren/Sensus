using Sensus.Models;

namespace Sensus.ViewModels.Factories
{
    public interface IInspectorViewModelFactory
    {
        InspectorViewModel Create(AcquisitionState state);
    }
}
