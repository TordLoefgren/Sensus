using Microsoft.Extensions.DependencyInjection;
using Sensus.Models;
using Sensus.Services;

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
            var state = new AcquisitionState();
            var serialConnection = ActivatorUtilities.CreateInstance<SerialConnectionService>(_serviceProvider);

            try
            {
                var acquisition = ActivatorUtilities.CreateInstance<AcquisitionService>(
                    _serviceProvider,
                    state,
                    serialConnection
                );

                return ActivatorUtilities.CreateInstance<WorkspaceViewModel>(
                    _serviceProvider,
                    state,
                    acquisition,
                    serialConnection
                );
            }
            catch
            {
                serialConnection.Dispose();

                throw;
            }
        }
    }
}
