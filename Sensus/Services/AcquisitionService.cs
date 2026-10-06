using System.IO;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Protocol;
using Sensus.Readers;

namespace Sensus.Services
{
    public class AcquisitionService : IAcquisitionService
    {
        private readonly AcquisitionState _state;
        private readonly IScannerProtocolService _scannerProtocolService;
        private readonly IScannerSimulationService _scannerSimulationService;
        private readonly ISerialConnectionService _serialConnectionService;
        private readonly SemaphoreSlim _transitionGate = new(1, 1);

        private volatile AcquisitionRun? _currentRun;

        public AcquisitionService(
            AcquisitionState state,
            IScannerProtocolService scannerProtocolService,
            IScannerSimulationService scannerSimulationService,
            ISerialConnectionService serialConnectionService
        )
        {
            _state = state;
            _scannerProtocolService = scannerProtocolService;
            _scannerSimulationService = scannerSimulationService;
            _serialConnectionService = serialConnectionService;
        }

        public Task RunSerialAsync(string portName, int baudRate)
            => RunAsync(() => _serialConnectionService.Open(portName, baudRate), SourceType.Serial);

        public Task RunSimulationAsync(SimulationScenario scenario, ScannerConfiguration configuration)
            => RunAsync(() => _scannerSimulationService.Create(scenario, configuration), SourceType.Simulation);

        private async Task RunAsync(Func<Stream> createStream, SourceType sourceType)
        {
            AcquisitionRun run;

            await _transitionGate.WaitAsync();

            try
            {
                await StopCurrentRunAsync();
                run = StartInputProcessing(createStream(), sourceType);
            }
            finally
            {
                _transitionGate.Release();
            }

            await run.ProcessingTask;
        }

        public void ClearSession()
        {
            if (_state.SourceState != SourceState.Idle)
            {
                return;
            }

            _state.Session = null;
        }

        private async Task ProcessInputAsync(AcquisitionRun run)
        {
            // Let the caller save the task before we continue processing.
            await Task.Yield();

            var stream = run.Stream;
            var cancellationToken = run.Cancellation.Token;

            try
            {
                using StreamReader reader = new(stream, leaveOpen: true);
                await using StreamWriter writer = new(stream, leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };
                run.CommandWriter = writer;

                var handshake = await _scannerProtocolService.PerformHandshakeAsync(reader, writer, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var session = new ScannerSession(handshake.Definition, handshake.Configuration);

                await _scannerProtocolService.StartScannerAsync(writer, cancellationToken);

                if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_currentRun, run))
                {
                    return;
                }

                _state.Session = session;
                _state.SourceState = SourceState.Active;

                await foreach (
                    var sample in RangeSampleReader.ReadSamplesAsync(
                        reader,
                        cancellationToken,
                        line =>
                        {
                            if (line == ScannerProtocolMessages.Stopped)
                            {
                                run.RecordStopResponseIfRequested();
                            }
                        }
                    )
                )
                {
                    var observation = RangeObservation.FromSample(sample, session.Definition);

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
                run.CommandWriter = null;
                run.StopResponse.TrySetResult(false);

                try
                {
                    await CloseInputTransportAsync(run);
                }
                finally
                {
                    if (ReferenceEquals(_currentRun, run))
                    {
                        _state.SourceState = SourceState.Idle;
                        _state.SourceType = SourceType.None;
                        _currentRun = null;
                    }

                    run.Cancellation.Dispose();
                }
            }
        }

        private AcquisitionRun StartInputProcessing(Stream stream, SourceType sourceType)
        {
            var run = new AcquisitionRun(stream, sourceType);
            _currentRun = run;

            _state.SourceType = sourceType;
            _state.SourceState = SourceState.Connecting;

            _state.Session = null;

            run.ProcessingTask = ProcessInputAsync(run);
            return run;
        }

        private async Task CloseInputTransportAsync(AcquisitionRun run)
        {
            if (run.SourceType == SourceType.Simulation)
            {
                await run.Stream.DisposeAsync();
                return;
            }

            _serialConnectionService.Close();
        }

        public async Task StopAsync()
        {
            await _transitionGate.WaitAsync();

            try
            {
                await StopCurrentRunAsync();
            }
            finally
            {
                _transitionGate.Release();
            }
        }

        private async Task StopCurrentRunAsync()
        {
            var run = _currentRun;
            if (run is null)
            {
                return;
            }

            var writer = run.CommandWriter;
            var requestStop = _state.SourceState == SourceState.Active &&
                writer is not null && !run.StopResponse.Task.IsCompleted;

            try
            {
                if (requestStop)
                {
                    run.RequestStop();

                    using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                    try
                    {
                        await _scannerProtocolService.StopScannerAsync(writer!, stopTimeout.Token);
                        if (!await run.StopResponse.Task.WaitAsync(stopTimeout.Token))
                        {
                            throw new EndOfStreamException("The scanner disconnected before responding to STOP.");
                        }
                    }
                    catch (OperationCanceledException ex) when (stopTimeout.IsCancellationRequested)
                    {
                        throw new TimeoutException("Scanner STOP timed out.", ex);
                    }
                }
            }
            finally
            {
                try
                {
                    run.Cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The input task already completed and disposed its cancellation source.
                }

                try
                {
                    if (run.SourceType == SourceType.Serial)
                    {
                        // Closing the serial port unblocks a pending read.
                        await CloseInputTransportAsync(run);
                    }
                }
                finally
                {
                    await run.ProcessingTask;
                }
            }
        }

        private class AcquisitionRun
        {
            private volatile bool _stopRequested;

            public Stream Stream { get; }
            public SourceType SourceType { get; }
            public CancellationTokenSource Cancellation { get; } = new();
            public Task ProcessingTask { get; set; } = null!;
            public volatile StreamWriter? CommandWriter;
            public TaskCompletionSource<bool> StopResponse { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public AcquisitionRun(Stream stream, SourceType sourceType)
            {
                Stream = stream;
                SourceType = sourceType;
            }

            public void RequestStop() => _stopRequested = true;

            public void RecordStopResponseIfRequested()
            {
                if (_stopRequested)
                {
                    StopResponse.TrySetResult(true);
                }
            }
        }
    }
}
