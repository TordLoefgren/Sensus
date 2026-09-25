using Sensus.Models;

namespace Sensus.ViewModels.Factories
{
    public interface IViewportViewModelFactory
    {
        ViewportViewModel Create(AcquisitionState state);
    }
}
