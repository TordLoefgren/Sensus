using System.IO;
using Sensus.Models;
using Sensus.Models.Enums;

namespace Sensus.Services
{
    public class AcquisitionService : IAcquisitionService
    {
        private readonly AcquisitionState _state;
        private readonly IRangeSampleReaderService _rangeSampleReaderService;
        private readonly IRangeObservationService _rangeObservationService;
        private readonly IScannerProtocolService _scannerProtocolService;
        private readonly IScannerSimulationService _scannerSimulationService;
        private readonly ISerialConnectionService _serialConnectionService;

        private CancellationTokenSource? _inputProcessingCancellationTokenSource;
        private Task? _inputProcessingTask;
        private Stream? _inputProcessingStream;

        public AcquisitionService(
            AcquisitionState state,
            IRangeSampleReaderService rangeSampleReaderService,
            IRangeObservationService rangeObservationService,
            IScannerProtocolService scannerProtocolService,
            IScannerSimulationService scannerSimulationService,
            ISerialConnectionService serialConnectionService)
        {
            _state = state;
            _rangeSampleReaderService = rangeSampleReaderService;
            _rangeObservationService = rangeObservationService;
            _scannerProtocolService = scannerProtocolService;
            _scannerSimulationService = scannerSimulationService;
            _serialConnectionService = serialConnectionService;
        }

        public async Task RunSerialAsync(string portName, int baudRate)
        {
            await StopAsync();
            _serialConnectionService.Close();

            var stream = _serialConnectionService.Open(portName, baudRate);
            await StartInputProcessing(stream, SourceType.Serial);
        }

        public async Task RunSimulationAsync(SimulationScenario scenario, ScannerConfiguration configuration)
        {
            await StopAsync();
            _serialConnectionService.Close();

            var stream = _scannerSimulationService.Create(scenario, configuration);
            await StartInputProcessing(stream, SourceType.Simulation, configuration);
        }

        public void ClearSession()
        {
            if (_state.SourceState != SourceState.Idle)
            {
                return;
            }

            _state.Session = null;
        }

        private async Task ProcessInputAsync(
            Stream stream, SourceType sourceType,
            ScannerConfiguration? simulationConfiguration,
            CancellationToken cancellationToken
        )
        {
            // Let StartInputProcessing retain the task before completion can clean it up.
            await Task.Yield();

            try
            {
                using StreamReader reader = new(stream, leaveOpen: true);
                await using StreamWriter writer = new(stream, leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                var handshake = await _scannerProtocolService.PerformHandshakeAsync(reader, writer, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // The simulator's requested settings describe its generated observations.
                // Serial sessions use the configuration returned by the handshake.
                var session = new ScannerSession(handshake.Definition, simulationConfiguration ?? handshake.Configuration);

                await _scannerProtocolService.StartScannerAsync(writer, cancellationToken);

                if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_inputProcessingStream, stream))
                {
                    return;
                }

                _state.Session = session;
                _state.SourceState = SourceState.Active;

                await foreach (var sample in _rangeSampleReaderService.ReadSamplesAsync(reader, cancellationToken))
                {
                    var observation = _rangeObservationService.CreateObservation(sample, session.Definition);

                    if (!cancellationToken.IsCancellationRequested && ReferenceEquals(_state.Session, session))
                    {
                        session.AddObservation(observation);
                    }
                }
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested &&
                ex is OperationCanceledException or ObjectDisposedException or IOException
            )
            {
                // Closing the transport unblocks pending reads during cancellation.
            }
            finally
            {
                // Explicit stop owns cleanup once it has detached this stream. EOF and
                // failures come through here and must release their transport as well.
                if (ReferenceEquals(_inputProcessingStream, stream))
                {
                    var cancellation = _inputProcessingCancellationTokenSource;
                    try
                    {
                        await CloseInputTransportAsync(stream, sourceType);
                    }
                    finally
                    {
                        if (ReferenceEquals(_inputProcessingStream, stream))
                        {
                            _inputProcessingStream = null;
                            _inputProcessingTask = null;
                            _inputProcessingCancellationTokenSource = null;

                            _state.SourceState = SourceState.Idle;
                            _state.SourceType = SourceType.None;
                        }
                        cancellation?.Dispose();
                    }
                }
            }
        }

        private Task StartInputProcessing(Stream stream, SourceType sourceType, ScannerConfiguration? simulationConfiguration = null)
        {
            _inputProcessingCancellationTokenSource = new();
            _inputProcessingStream = stream;

            _state.SourceType = sourceType;
            _state.SourceState = SourceState.Connecting;

            _state.Session = null;

            _inputProcessingTask = ProcessInputAsync(stream, sourceType, simulationConfiguration, _inputProcessingCancellationTokenSource.Token);
            return _inputProcessingTask;
        }

        private async Task CloseInputTransportAsync(Stream? stream, SourceType sourceType)
        {
            if (stream is null)
            {
                return;
            }

            if (sourceType == SourceType.Simulation)
            {
                await stream.DisposeAsync();
                return;
            }

            _serialConnectionService.Close();
        }

        public async Task StopAsync()
        {
            // Snapshot the previous session to make sure cleanup cannot target the replacement.
            var task = _inputProcessingTask;
            var cancellationTokenSource = _inputProcessingCancellationTokenSource;
            var stream = _inputProcessingStream;
            var sourceType = _state.SourceType;

            _inputProcessingTask = null;
            _inputProcessingCancellationTokenSource = null;
            _inputProcessingStream = null;

            _state.SourceState = SourceState.Idle;
            _state.SourceType = SourceType.None;

            if (task is null)
            {
                return;
            }

            cancellationTokenSource?.Cancel();

            try
            {
                if (sourceType == SourceType.Simulation)
                {
                    try
                    {
                        await task;
                    }
                    finally
                    {
                        await CloseInputTransportAsync(stream, sourceType);
                    }
                }
                else
                {
                    try
                    {
                        await CloseInputTransportAsync(stream, sourceType);
                    }
                    finally
                    {
                        await task;
                    }
                }
            }
            finally
            {
                cancellationTokenSource?.Dispose();
            }
        }

    }
}
