using System.Windows;
namespace PhoneMirror;
public static class PhoneGeometry
{
    public const double Frame = 8;
    public static double ScreenRadius(double width, double height) => Math.Min(width, height) * 0.112;
    public const double IPhone17Ratio = 1206d / 2622;
    public static double Ratio(string? size)
    {
        var parts = size?.Split('×', 'x');
        if (parts?.Length == 2 && double.TryParse(parts[0].Trim(), out var width) && double.TryParse(parts[1].Trim(), out var height) && width >= 100 && height >= 100)
            return Math.Clamp(width / height, 0.3, 3.34);
        return IPhone17Ratio;
    }
    public static Rect Island(double width, double height)
    {
        var shortSide = Math.Min(width, height);
        var pillWidth = shortSide * 0.29; var pillHeight = shortSide * 0.079; var inset = shortSide * 0.033;
        return width <= height ? new Rect((width - pillWidth) / 2, inset, pillWidth, pillHeight) : new Rect(inset, (height - pillWidth) / 2, pillHeight, pillWidth);
    }
    public static Size Fit(double ratio, double maxWidth, double maxHeight)
    {
        var height = Math.Min(maxHeight - 16, (maxWidth - 16) / ratio);
        height = Math.Max(100, height);
        return new Size(height * ratio + 16, height + 16);
    }
}
