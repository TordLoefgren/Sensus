using System.Collections.ObjectModel;
using System.ComponentModel;
using Sensus.Extensions;
using Sensus.Models;
using Sensus.Models.Enums;
using Sensus.Services;

namespace Sensus.ViewModels
{
    public class ScannerViewModel : ObservableObject, IDisposable
    {

        #region State

        private readonly IAcquisitionService _acquisitionService;

        public AcquisitionState State { get; }

        public bool CanClearSession => State.Session is not null && State.SourceState == SourceState.Idle;

        #endregion

        #region Serial Connection

        public const int DefaultBaudRate = 9600;
        private const string PreferredSerialPortName = "COM6";

        private readonly ISerialConnectionService _serialConnectionService;

        public bool CanUseSerial => State.SourceState == SourceState.Idle || State.SourceType == SourceType.Serial;

        public ObservableCollection<string> AvailablePortNames { get; } = [];

        private string? _selectedPortName;
        public string? SelectedPortName
        {
            get => _selectedPortName;
            set => SetField(ref _selectedPortName, value);
        }

        public string SerialBaudRateDisplay => $"{DefaultBaudRate} baud";

        public string SerialConnectionButtonDisplay => State.SourceType == SourceType.Serial
            ? State.SourceState switch
            {
                SourceState.Connecting => "Cancel",
                SourceState.Active => "Disconnect",
                _ => "Connect"
            }
            : "Connect";

        #endregion

        #region Simulation

        public bool CanUseSimulation => State.SourceState == SourceState.Idle || State.SourceType == SourceType.Simulation;

        public SimulationScenario[] AvailableSimulationScenarios { get; } = Enum.GetValues<SimulationScenario>();

        private SimulationScenario _selectedSimulationScenario = SimulationScenario.SymmetricSweep;
        public SimulationScenario SelectedSimulationScenario
        {
            get => _selectedSimulationScenario;
            set => SetField(ref _selectedSimulationScenario, value);
        }

        public string SimulationButtonDisplay => State.SourceType == SourceType.Simulation
            ? State.SourceState switch
            {
                SourceState.Connecting => "Cancel",
                SourceState.Active => "Stop",
                _ => "Start"
            }
            : "Start";

        #endregion

        #region Scanner Definition

        public string ScannerIdentityDisplay => State.Session?.Definition is { } definition
            ? string.IsNullOrWhiteSpace(definition.Mark)
                ? definition.Name
                : $"{definition.Name} · {definition.Mark}"
            : "No scanner connected";

        public string BoardNameDisplay => State.Session?.Definition.MicrocontrollerBoard.Name ?? "-";

        public string MicrocontrollerDisplay => State.Session?.Definition.MicrocontrollerBoard.Microcontroller ?? "-";

        public string SensorNameDisplay => State.Session?.Definition.RangeSensor.Name ?? "-";

        public string SensorRangeDisplay => State.Session?.Definition is { } definition
            ? $"{definition.RangeSensor.MinRangeCm:0.##} – {definition.RangeSensor.MaxRangeCm:0.##} cm"
            : "-";

        public string SensorMeasuringAngleDisplay => State.Session?.Definition is { } definition
            ? $"{definition.RangeSensor.MeasuringAngleDegrees:0.##}°"
            : "-";

        public string ServoNameDisplay => State.Session?.Definition.ServoMotor.Name ?? "-";

        public string ServoRotationRangeDisplay => State.Session?.Definition is { } definition
            ? $"{definition.ServoMotor.RotationRangeDegrees:0.##}°"
            : "-";

        #endregion

        #region Scanner Configuration

        public string SweepRangeDisplay => State.Session?.Configuration is { } configuration
            ? $"{configuration.MinBearingDegrees:0.##}° ↔ {configuration.MaxBearingDegrees:0.##}°"
            : "-";

        public string BearingStepDisplay => State.Session?.Configuration is { } configuration
            ? $"{configuration.BearingStepDegrees:0.##}°"
            : "-";

        public string AcquisitionDelayDisplay => State.Session?.Configuration is { } configuration
            ? $"{configuration.AcquisitionDelayMs} ms"
            : "-";

        public string EchoTimeoutDisplay => State.Session?.Configuration is { } configuration
            ? $"{configuration.EchoTimeoutUs} μs"
            : "-";

        #endregion

        #region Errors

        public string? ErrorMessage => _errorMessage;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        private string? _errorMessage;

        #endregion

        public ScannerViewModel(
            AcquisitionState state,
            ISerialConnectionService serialConnectionService,
            IAcquisitionService acquisitionService
        )
        {
            State = state;

            _serialConnectionService = serialConnectionService;
            _acquisitionService = acquisitionService;

            PropertyChangedEventManager.AddHandler(State, OnSourceChanged, nameof(AcquisitionState.SourceState));
            PropertyChangedEventManager.AddHandler(State, OnSourceChanged, nameof(AcquisitionState.SourceType));
            PropertyChangedEventManager.AddHandler(State, OnSessionChanged, nameof(AcquisitionState.Session));

            try
            {
                UpdateSerialPorts(_serialConnectionService.GetPortNames());
            }
            catch (Exception ex)
            {
                UpdateErrorMessage(ex.Message);
            }
        }

        #region Serial Connection Actions

        public async Task RefreshSerialPortsAsync()
        {
            UpdateErrorMessage(null);
            try
            {
                var portNames = _serialConnectionService.GetPortNames();
                if (_serialConnectionService.IsOpen && !portNames.Contains(_serialConnectionService.PortName))
                {
                    await _acquisitionService.StopAsync();
                }

                UpdateSerialPorts(portNames);
            }
            catch (Exception ex)
            {
                UpdateErrorMessage(ex.Message);
            }
        }

        public async Task ToggleSerialConnectionAsync()
        {
            if (!CanUseSerial)
            {
                return;
            }

            UpdateErrorMessage(null);

            try
            {
                if (State.SourceState != SourceState.Idle)
                {
                    await _acquisitionService.StopAsync();
                    return;
                }

                if (SelectedPortName is not { } portName)
                {
                    UpdateErrorMessage("Please select a serial port");
                    return;
                }

                await _acquisitionService.RunSerialAsync(portName, DefaultBaudRate);
            }
            catch (Exception ex)
            {
                UpdateErrorMessage(ex.Message);
            }
        }

        private void UpdateSerialPorts(string[] portNames)
        {
            var selectedPortName = SelectedPortName;
            AvailablePortNames.Clear();
            AvailablePortNames.AddRange(portNames);
            SelectedPortName = portNames.Contains(selectedPortName)
                ? selectedPortName
                : portNames.Contains(PreferredSerialPortName)
                    ? PreferredSerialPortName
                    : portNames.FirstOrDefault();
        }

        #endregion

        #region Acquisition Actions

        public async Task ToggleSimulationAsync()
        {
            if (!CanUseSimulation)
            {
                return;
            }

            UpdateErrorMessage(null);
            try
            {
                if (State.SourceState == SourceState.Idle)
                {
                    await _acquisitionService.RunSimulationAsync(SelectedSimulationScenario, ScannerConfiguration.Default);
                }
                else
                {
                    await _acquisitionService.StopAsync();
                }
            }
            catch (Exception ex)
            {
                UpdateErrorMessage(ex.Message);
            }
        }

        public void ClearSession()
        {
            if (CanClearSession)
            {
                _acquisitionService.ClearSession();
            }
        }

        #endregion

        public void Dispose()
        {
            PropertyChangedEventManager.RemoveHandler(State, OnSourceChanged, nameof(AcquisitionState.SourceState));
            PropertyChangedEventManager.RemoveHandler(State, OnSourceChanged, nameof(AcquisitionState.SourceType));
            PropertyChangedEventManager.RemoveHandler(State, OnSessionChanged, nameof(AcquisitionState.Session));
        }

        #region State Updates

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanClearSession));
            OnPropertyChanged(nameof(ScannerIdentityDisplay));
            OnPropertyChanged(nameof(BoardNameDisplay));
            OnPropertyChanged(nameof(MicrocontrollerDisplay));
            OnPropertyChanged(nameof(SensorNameDisplay));
            OnPropertyChanged(nameof(SensorRangeDisplay));
            OnPropertyChanged(nameof(SensorMeasuringAngleDisplay));
            OnPropertyChanged(nameof(ServoNameDisplay));
            OnPropertyChanged(nameof(ServoRotationRangeDisplay));

            OnPropertyChanged(nameof(SweepRangeDisplay));
            OnPropertyChanged(nameof(BearingStepDisplay));
            OnPropertyChanged(nameof(AcquisitionDelayDisplay));
            OnPropertyChanged(nameof(EchoTimeoutDisplay));
        }

        private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanUseSerial));
            OnPropertyChanged(nameof(CanUseSimulation));
            OnPropertyChanged(nameof(CanClearSession));
            OnPropertyChanged(nameof(SerialConnectionButtonDisplay));
            OnPropertyChanged(nameof(SimulationButtonDisplay));
        }

        private void UpdateErrorMessage(string? message)
        {
            if (SetField(ref _errorMessage, message, nameof(ErrorMessage)))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }

        #endregion

    }
}
