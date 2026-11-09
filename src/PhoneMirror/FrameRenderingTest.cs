using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhoneMirror;

public static class FrameRenderingTest
{
    public static void Run()
    {
        foreach (var (width, scale) in new[] { (220d, 1d), (365d, 1.25d), (480d, 1.5d) })
        {
            var height = (width - 16) / PhoneGeometry.IPhone17Ratio + 16;
            var frame = new FrameArtwork();
            frame.Measure(new Size(width, height)); frame.Arrange(new Rect(0, 0, width, height)); frame.UpdateLayout();
            var pixelWidth = (int)Math.Ceiling(width * scale); var pixelHeight = (int)Math.Ceiling(height * scale);
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(frame);
            var pixels = new byte[pixelWidth * pixelHeight * 4]; bitmap.CopyPixels(pixels, pixelWidth * 4, 0);
            // A binary HRGN has no fractional coverage. The new frame must have partially transparent outer pixels.
            var partialAlpha = 0;
            for (var y = 0; y < pixelHeight; y++)
                for (var x = 0; x < pixelWidth; x++)
                {
                    var alpha = pixels[(y * pixelWidth + x) * 4 + 3];
                    if ((x < 3 || x >= pixelWidth - 3 || y < pixelWidth / 5 || y >= pixelHeight - pixelWidth / 5) && alpha is > 0 and < 255) partialAlpha++;
                }
            if (partialAlpha < 50) throw new Exception($"Frame has no smooth alpha edge at scale {scale}");
            if (pixels[3] != 0) throw new Exception("Outer corner is not transparent");
            if (pixels[((pixelHeight / 2) * pixelWidth + pixelWidth / 2) * 4 + 3] != 0) throw new Exception("Frame covers video center");
            if (width == 365)
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(AppContext.BaseDirectory, "frame-antialias-preview.png")); encoder.Save(file);
            }
        }
    }
}
