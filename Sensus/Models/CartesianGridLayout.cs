namespace Sensus.Models
{
    public readonly record struct CartesianGridLayout(
        double FirstX,
        double FirstY,
        int VerticalCount,
        int HorizontalCount
    );
}
