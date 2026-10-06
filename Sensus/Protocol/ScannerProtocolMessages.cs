using System.Globalization;

namespace Sensus.Protocol
{
    public static class ScannerProtocolMessages
    {
        public const int Revision = 1;

        private static readonly string Prefix = "SENSUS," + Revision.ToString(CultureInfo.InvariantCulture) + ",";

        public static readonly string Prepare = Prefix + "PREPARE";
        public static readonly string Description = Prefix + "DESCRIPTION";
        public static readonly string Ready = Prefix + "READY";
        public static readonly string Start = Prefix + "START";
        public static readonly string Stop = Prefix + "STOP";
        public static readonly string Stopped = Prefix + "STOPPED";
    }
}
