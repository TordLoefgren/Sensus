using System.IO;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Serializers;
using Sensus.Simulation;

namespace Sensus.Services
{
    public class ScannerSimulationService : IScannerSimulationService
    {
        private static readonly ScannerDefinition SimulationDefinition = new(
            "Sensus Rover Simulation",
            "Mark 1-A",
            new("Simulated UNO R3", "ATmega328"),
            new("Simulated HC-SR04", 2.0, 400.0, 15.0),
            new("Simulated SG90", 180.0)
        );

        public Stream Create(SimulationScenario scenario, ScannerConfiguration configuration)
        {
            var response = ScannerHandshakeResponseSerializer.Serialize(new(SimulationDefinition, configuration));
            return new ScannerSimulationStream(
                ScannerSimulationGenerator.Create(scenario, configuration),
                response
            );
        }
    }
}
