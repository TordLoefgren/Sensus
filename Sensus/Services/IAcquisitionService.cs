using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Services
{
    public interface IAcquisitionService
    {
        Task RunSerialAsync(string portName, int baudRate);

        Task RunSimulationAsync(SimulationScenario scenario, ScannerConfiguration configuration);

        Task StopAsync();
        void ClearSession();
    }
}
