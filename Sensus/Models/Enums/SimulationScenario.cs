using System.ComponentModel;

namespace Sensus.Models.Enums
{
    public enum SimulationScenario : byte
    {
        [Description("Constant Sweep")]
        ConstantSweep = 0,

        [Description("Symmetric Sweep")]
        SymmetricSweep = 1,

        [Description("Moving Sweep")]
        MovingSweep = 2,

        [Description("Limit Sweep")]
        LimitSweep = 3
    }
}
