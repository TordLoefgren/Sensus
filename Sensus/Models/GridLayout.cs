namespace Sensus.Models
{
    public readonly record struct GridLayout(
        double FirstX,
        double FirstY,
        int VerticalCount,
        int HorizontalCount
    );
}
