using System.ComponentModel;

namespace Sensus.Enums
{
    public enum SampleStatus : byte
    {
        [Description("Valid")]
        Valid = 0,
        [Description("No echo")]
        NoEcho = 1
    }
}
