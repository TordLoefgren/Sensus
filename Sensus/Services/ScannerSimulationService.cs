using System.IO;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Simulation;

namespace Sensus.Services
{
    public class ScannerSimulationService : IScannerSimulationService
    {
        private readonly IRangeSampleSerializerService _rangeSampleSerializerService;

        public ScannerSimulationService(IRangeSampleSerializerService rangeSampleSerializerService)
        {
            _rangeSampleSerializerService = rangeSampleSerializerService;
        }

        public Stream Create(SimulationScenario scenario, ScannerConfiguration configuration)
        {
            return new ScannerSimulationStream(
                ScannerSimulationGenerator.Create(scenario, configuration), _rangeSampleSerializerService
            );
        }
    }
}
