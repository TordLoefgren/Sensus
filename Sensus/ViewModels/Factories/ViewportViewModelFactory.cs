using Sensus.Models;

namespace Sensus.ViewModels.Factories
{
    public class ViewportViewModelFactory : IViewportViewModelFactory
    {
        public ViewportViewModel Create(AcquisitionState state)
        {
            return new(state);
        }
    }
}
