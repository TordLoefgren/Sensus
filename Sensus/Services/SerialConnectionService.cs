using System.IO;
using System.IO.Ports;

namespace Sensus.Services
{
    public class SerialConnectionService : ISerialConnectionService
    {
        private readonly SerialPort _serialPort = new() { NewLine = "\r\n" };

        public string[] GetPortNames() => SerialPort.GetPortNames();
        public bool IsOpen => _serialPort.IsOpen;
        public string PortName => _serialPort.PortName;

        public Stream Open(string portName, int baudRate)
        {
            _serialPort.PortName = portName;
            _serialPort.BaudRate = baudRate;
            _serialPort.Open();

            return _serialPort.BaseStream;
        }

        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        public void Dispose()
        {
            _serialPort.Dispose();
        }
    }
}
