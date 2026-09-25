namespace Sensus.Models
{
    public readonly record struct ViewportTransform(
        double Left,
        double Top,
        double Width,
        double Height,
        double OriginScreenX,
        double OriginScreenY,
        double CmToPixels
    )
    {
        public double WorldToScreenX(double worldX)
        {
            return OriginScreenX + worldX * CmToPixels;
        }

        public double WorldToScreenY(double worldY)
        {
            return OriginScreenY - worldY * CmToPixels;
        }

        public double ScreenToWorldX(double screenX)
        {
            return (screenX - OriginScreenX) / CmToPixels;
        }

        public double ScreenToWorldY(double screenY)
        {
            return (OriginScreenY - screenY) / CmToPixels;
        }
    }
}
