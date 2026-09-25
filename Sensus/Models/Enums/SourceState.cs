using System.ComponentModel;

namespace Sensus.Models.Enums
{
    public enum SourceState : byte
    {
        [Description("Idle")]
        Idle = 0,

        [Description("Connecting")]
        Connecting = 1,

        [Description("Active")]
        Active = 2
    }
}
