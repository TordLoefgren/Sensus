using Sensus.ViewModels.Factories;

namespace Sensus.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        public WorkspaceViewModel Workspace { get; }

        public MainViewModel(IWorkspaceViewModelFactory workspaceViewModelFactory)
        {
            Workspace = workspaceViewModelFactory.Create();
        }
    }
}
