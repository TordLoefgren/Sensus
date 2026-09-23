namespace Sensus.Models
{
    public readonly record struct MicrocontrollerBoard(
        string Name,
        string Microcontroller
    );

    public readonly record struct RangeSensor(
        string Name,
        double MinRangeCm,
        double MaxRangeCm,
        double MeasuringAngleDegrees
    );

    public readonly record struct ServoMotor(
        string Name,
        double RotationRangeDegrees
    );

    public readonly record struct ScannerDefinition(
        string Name,
        string Mark,
        MicrocontrollerBoard MicrocontrollerBoard,
        RangeSensor RangeSensor,
        ServoMotor ServoMotor
    );
}
