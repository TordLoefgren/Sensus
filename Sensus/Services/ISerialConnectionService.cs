using System.IO;

namespace Sensus.Services
{
    public interface ISerialConnectionService : IDisposable
    {
        string[] GetPortNames();

        bool IsOpen { get; }
        string? PortName { get; }

        Stream Open(string portName, int baudRate);
        void Close();
    }
}
