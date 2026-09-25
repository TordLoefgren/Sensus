using System.ComponentModel;

namespace Sensus.Models.Enums
{
    public enum RangeStatus
    {
        [Description("In range")]
        InRange,

        [Description("Too close")]
        TooClose,

        [Description("Too far")]
        TooFar
    }
}
