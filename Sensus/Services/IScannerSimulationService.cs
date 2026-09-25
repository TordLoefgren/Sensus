using System.IO;
using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Services
{
    public interface IScannerSimulationService
    {
        Stream Create(SimulationScenario scenario, ScannerConfiguration configuration);
    }
}
