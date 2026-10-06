using System.Globalization;
using Sensus.Models;
using Sensus.Protocol;

namespace Sensus.Serializers
{
    public static class ScannerHandshakeResponseSerializer
    {
        public static int LineCount => 7;

        private const string NewLine = "\r\n";

        /// <summary>Serializes a complete handshake response, including its final CRLF.</summary>
        public static string Serialize(ScannerHandshakeResponse value)
        {
            var definition = value.Definition;
            var configuration = value.Configuration;

            return string.Join(
                NewLine,
                ScannerProtocolMessages.Description,
                WriteRow(
                    "SCANNER",
                    definition.Name,
                    definition.Mark
                ),
                WriteRow(
                    "BOARD",
                    definition.MicrocontrollerBoard.Name,
                    definition.MicrocontrollerBoard.Microcontroller
                ),
                WriteRow(
                    "RANGE_SENSOR",
                    definition.RangeSensor.Name,
                    definition.RangeSensor.MinRangeCm,
                    definition.RangeSensor.MaxRangeCm,
                    definition.RangeSensor.MeasuringAngleDegrees
                ),
                WriteRow(
                    "SERVO",
                    definition.ServoMotor.Name,
                    definition.ServoMotor.RotationRangeDegrees
                ),
                WriteRow(
                    "CONFIGURATION",
                    configuration.MinBearingDegrees,
                    configuration.MaxBearingDegrees,
                    configuration.BearingStepDegrees,
                    configuration.AcquisitionDelayMs,
                    configuration.EchoTimeoutUs
                ),
                ScannerProtocolMessages.Ready,
                string.Empty // Terminate the final line as well.
            );
        }

        /// <summary>Parses a complete handshake response, with or without its final CRLF.</summary>
        public static bool TryDeserialize(string value, out ScannerHandshakeResponse outValue)
        {
            outValue = default;

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            // Accept a complete block with or without its final CRLF.
            if (value.EndsWith(NewLine, StringComparison.Ordinal))
            {
                value = value[..^NewLine.Length];
            }

            var lines = value.Split(NewLine, StringSplitOptions.None);

            if (lines.Length != LineCount ||
                lines[0] != ScannerProtocolMessages.Description ||
                lines[^1] != ScannerProtocolMessages.Ready
            )
            {
                return false;
            }

            if (!TryReadRow(lines[1], "SCANNER", 3, out var scanner) ||
                !TryReadRow(lines[2], "BOARD", 3, out var board) ||
                !TryReadRow(lines[3], "RANGE_SENSOR", 5, out var sensor) ||
                !TryReadRow(lines[4], "SERVO", 3, out var servo) ||
                !TryReadRow(lines[5], "CONFIGURATION", 6, out var configuration)
            )
            {
                return false;
            }

            if (!TryReadDouble(sensor[2], out var minRangeCm) ||
                !TryReadDouble(sensor[3], out var maxRangeCm) ||
                !TryReadDouble(sensor[4], out var measuringAngleDegrees) ||
                !TryReadDouble(servo[2], out var rotationRangeDegrees) ||
                !TryReadDouble(configuration[1], out var minBearingDegrees) ||
                !TryReadDouble(configuration[2], out var maxBearingDegrees) ||
                !TryReadDouble(configuration[3], out var bearingStepDegrees) ||
                !uint.TryParse(
                    configuration[4],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var acquisitionDelayMs
                ) ||
                !uint.TryParse(
                    configuration[5],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var echoTimeoutUs
                )
            )
            {
                return false;
            }

            outValue = new(
                new(
                    scanner[1],
                    scanner[2],
                    new(board[1], board[2]),
                    new(
                        sensor[1],
                        minRangeCm,
                        maxRangeCm,
                        measuringAngleDegrees
                    ),
                    new(servo[1], rotationRangeDegrees)
                ),
                new(
                    minBearingDegrees,
                    maxBearingDegrees,
                    bearingStepDegrees,
                    acquisitionDelayMs,
                    echoTimeoutUs
                )
            );

            return true;
        }

        private static string WriteRow(params object[] fields)
        {
            return string.Join(",", fields.Select(FormatField));
        }

        private static string FormatField(object? value)
        {
            if (value is double number && !double.IsFinite(number))
            {
                throw new ArgumentException("Handshake numbers must be finite.", nameof(value));
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!IsValidField(text))
            {
                throw new ArgumentException("Handshake fields must be nonempty and cannot contain commas or line breaks.", nameof(value));
            }

            return text!;
        }

        private static bool TryReadRow(
            string line,
            string label,
            int fieldCount,
            out string[] fields
        )
        {
            fields = line.Split(',');

            // fieldCount includes the label.
            return fields.Length == fieldCount && fields[0] == label && fields.All(IsValidField);
        }

        private static bool IsValidField(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOfAny([',', '\r', '\n']) < 0;
        }

        private static bool TryReadDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && double.IsFinite(result);
        }
    }
}
