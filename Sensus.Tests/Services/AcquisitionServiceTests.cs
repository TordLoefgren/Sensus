using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Serializers;
using Sensus.Services;
using Sensus.Simulation;

namespace Sensus.Tests.Services
{
    public class AcquisitionServiceTests
    {
        [Fact]
        public async Task StopAsync_PreservesSession_When_ActiveSimulationRespondsToStop()
        {
            // Arrange.
            var state = new AcquisitionState();
            using var serial = new SerialConnectionService();
            var acquisition = CreateAcquisition(state, serial);

            // A long sample delay verifies that STOP interrupts an active read.
            var configuration = new ScannerConfiguration(-45, 45, 15, 10_000, 30_000);

            // Act.
            var runTask = acquisition.RunSimulationAsync(SimulationScenario.ConstantSweep, configuration);

            try
            {
                await WaitUntilActiveAsync(state);

                // Assert.
                var session = Assert.IsType<ScannerSession>(state.Session);
                Assert.Equal(configuration, session.Configuration);

                await acquisition.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
                await runTask.WaitAsync(TimeSpan.FromSeconds(3));

                Assert.Equal(SourceState.Idle, state.SourceState);
                Assert.Same(session, state.Session);
                Assert.Empty(session.Observations);
            }
            finally
            {
                await acquisition.StopAsync();
            }
        }

        [Fact]
        public async Task StopAsync_WaitsForFirstStop_When_CalledAgain_And_StopResponseIsPending()
        {
            // Arrange.
            var state = new AcquisitionState();
            using var serial = new SerialConnectionService();
            var stopEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseStop = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var protocol = CreateBlockedStopProtocol(stopEntered, releaseStop);
            var acquisition = CreateAcquisition(state, serial, protocol: protocol);
            var runTask = acquisition.RunSimulationAsync(
                SimulationScenario.ConstantSweep,
                new ScannerConfiguration(-45, 45, 15, 10_000, 30_000)
            );

            try
            {
                await WaitUntilActiveAsync(state);

                // Act.
                var firstStop = acquisition.StopAsync();
                await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                var secondStop = acquisition.StopAsync();

                // Assert.
                Assert.False(secondStop.IsCompleted);
                Assert.Equal(SourceState.Active, state.SourceState);

                // Act.
                releaseStop.TrySetResult(true);
                await Task.WhenAll(firstStop, secondStop).WaitAsync(TimeSpan.FromSeconds(4));
                await runTask.WaitAsync(TimeSpan.FromSeconds(4));

                // Assert.
                Assert.Equal(SourceState.Idle, state.SourceState);
                Assert.Equal(SourceType.None, state.SourceType);
            }
            finally
            {
                releaseStop.TrySetResult(true);
                await acquisition.StopAsync();
            }
        }

        [Fact]
        public async Task RunSimulationAsync_WaitsToCreateNextStream_When_PreviousStopIsInProgress()
        {
            // Arrange.
            var state = new AcquisitionState();
            using var serial = new SerialConnectionService();
            var stopEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseStop = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var protocol = CreateBlockedStopProtocol(stopEntered, releaseStop);
            var actualSimulation = new ScannerSimulationService();
            var simulation = new TestSimulationService(actualSimulation.Create);
            var acquisition = CreateAcquisition(state, serial, protocol, simulation);
            var configuration = new ScannerConfiguration(-45, 45, 15, 10_000, 30_000);
            var firstRun = acquisition.RunSimulationAsync(SimulationScenario.ConstantSweep, configuration);

            try
            {
                await WaitUntilActiveAsync(state);
                var firstSession = state.Session;

                // Act.
                var stopTask = acquisition.StopAsync();
                await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                var secondRun = acquisition.RunSimulationAsync(SimulationScenario.ConstantSweep, configuration);

                // Assert.
                Assert.Equal(1, simulation.CreateCount);
                Assert.Equal(SourceState.Active, state.SourceState);

                // Act.
                releaseStop.TrySetResult(true);
                await stopTask.WaitAsync(TimeSpan.FromSeconds(4));
                await WaitUntilActiveAsync(state);

                // Assert.
                Assert.Equal(2, simulation.CreateCount);
                Assert.NotSame(firstSession, state.Session);

                await acquisition.StopAsync().WaitAsync(TimeSpan.FromSeconds(4));
                await Task.WhenAll(firstRun, secondRun).WaitAsync(TimeSpan.FromSeconds(4));
            }
            finally
            {
                releaseStop.TrySetResult(true);
                await acquisition.StopAsync();
            }
        }

        [Fact]
        public async Task StopAsync_ReleasesRun_When_StopResponseTimesOut()
        {
            // Arrange.
            var state = new AcquisitionState();
            using var serial = new SerialConnectionService();
            var actualProtocol = new ScannerProtocolService();
            var protocol = new TestProtocolService(actualProtocol, (_, _) => Task.CompletedTask);
            var acquisition = CreateAcquisition(state, serial, protocol: protocol);
            var runTask = acquisition.RunSimulationAsync(
                SimulationScenario.ConstantSweep,
                new ScannerConfiguration(-45, 45, 15, 10_000, 30_000)
            );

            try
            {
                await WaitUntilActiveAsync(state);

                // Act & Assert.
                await Assert.ThrowsAsync<TimeoutException>(acquisition.StopAsync);
                await runTask.WaitAsync(TimeSpan.FromSeconds(4));
                Assert.Equal(SourceState.Idle, state.SourceState);
                Assert.Equal(SourceType.None, state.SourceType);
            }
            finally
            {
                await acquisition.StopAsync();
            }
        }

        [Fact]
        public async Task RunSimulationAsync_ReleasesRun_When_SampleStreamEndsWithoutStop()
        {
            // Arrange.
            var state = new AcquisitionState();
            using var serial = new SerialConnectionService();
            var simulation = new TestSimulationService((_, configuration) =>
                new ScannerSimulationStream(
                    _ => NoSamples(),
                    ScannerHandshakeResponseSerializer.Serialize(
                        new(
                            new ScannerDefinition(
                                "Finite scanner",
                                "Test",
                                new("Board", "Microcontroller"),
                                new("Sensor", 2, 400, 15),
                                new("Servo", 180)
                            ),
                            configuration
                        )
                    )
                )
            );
            var acquisition = CreateAcquisition(state, serial, simulation: simulation);
            var configuration = new ScannerConfiguration(-45, 45, 15, 1, 30_000);

            try
            {
                // Act.
                await acquisition.RunSimulationAsync(SimulationScenario.ConstantSweep, configuration)
                    .WaitAsync(TimeSpan.FromSeconds(4));

                // Assert.
                Assert.Equal(SourceState.Idle, state.SourceState);
                Assert.Equal(SourceType.None, state.SourceType);
                Assert.Equal("Finite scanner", Assert.IsType<ScannerSession>(state.Session).Definition.Name);
                Assert.Equal(1, simulation.CreateCount);
            }
            finally
            {
                await acquisition.StopAsync();
            }
        }

        private static AcquisitionService CreateAcquisition(
            AcquisitionState state,
            SerialConnectionService serial,
            IScannerProtocolService? protocol = null,
            IScannerSimulationService? simulation = null
        )
        {
            return new(
                state,
                protocol ?? new ScannerProtocolService(),
                simulation ?? new ScannerSimulationService(),
                serial
            );
        }

        private static TestProtocolService CreateBlockedStopProtocol(
            TaskCompletionSource<bool> stopEntered,
            TaskCompletionSource<bool> releaseStop
        )
        {
            var actualProtocol = new ScannerProtocolService();
            return new(actualProtocol, async (writer, cancellationToken) =>
            {
                stopEntered.TrySetResult(true);
                await releaseStop.Task.WaitAsync(cancellationToken);
                await actualProtocol.StopScannerAsync(writer, cancellationToken);
            });
        }

        private static async Task WaitUntilActiveAsync(AcquisitionState state)
        {
            for (var attempt = 0; attempt < 200 && state.SourceState != SourceState.Active; attempt++)
            {
                await Task.Delay(10);
            }

            Assert.Equal(SourceState.Active, state.SourceState);
        }

        private static async IAsyncEnumerable<RangeSample> NoSamples()
        {
            await Task.Yield();
            yield break;
        }

        private class TestProtocolService : IScannerProtocolService
        {
            private readonly ScannerProtocolService _actualProtocol;
            private readonly Func<StreamWriter, CancellationToken, Task> _stop;

            public TestProtocolService(
                ScannerProtocolService actualProtocol,
                Func<StreamWriter, CancellationToken, Task> stop
            )
            {
                _actualProtocol = actualProtocol;
                _stop = stop;
            }

            public Task<ScannerHandshakeResponse> PerformHandshakeAsync(
                StreamReader reader,
                StreamWriter writer,
                CancellationToken cancellationToken
            ) => _actualProtocol.PerformHandshakeAsync(reader, writer, cancellationToken);

            public Task StartScannerAsync(StreamWriter writer, CancellationToken cancellationToken)
                => _actualProtocol.StartScannerAsync(writer, cancellationToken);

            public Task StopScannerAsync(StreamWriter writer, CancellationToken cancellationToken)
                => _stop(writer, cancellationToken);
        }

        private class TestSimulationService : IScannerSimulationService
        {
            private readonly Func<SimulationScenario, ScannerConfiguration, Stream> _create;
            private int _createCount;

            public int CreateCount => Volatile.Read(ref _createCount);

            public TestSimulationService(Func<SimulationScenario, ScannerConfiguration, Stream> create)
            {
                _create = create;
            }

            public Stream Create(SimulationScenario scenario, ScannerConfiguration configuration)
            {
                Interlocked.Increment(ref _createCount);
                return _create(scenario, configuration);
            }
        }
    }
}
