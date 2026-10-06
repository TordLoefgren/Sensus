using Microsoft.Extensions.DependencyInjection;

namespace Sensus.ViewModels.Factories
{
    public class WorkspaceViewModelFactory : IWorkspaceViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public WorkspaceViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public WorkspaceViewModel Create()
        {
            return ActivatorUtilities.CreateInstance<WorkspaceViewModel>(_serviceProvider);
        }
    }
}
