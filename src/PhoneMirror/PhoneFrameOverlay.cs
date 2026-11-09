using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PhoneMirror;

// Separate alpha-composited window: WPF cannot alpha-blend on top of a child video HWND.
// This layer contains only vector decoration; it never reads or copies video frames.
public sealed class PhoneFrameOverlay : Window
{
    private readonly Window phone;
    private readonly FrameArtwork artwork = new();
    public PhoneFrameOverlay(Window phone)
    {
        this.phone = phone;
        Owner = phone; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false;
        ShowActivated = false; Focusable = false; IsHitTestVisible = false;
        Title = "Поток · сглаженный контур"; Content = artwork;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = Native.GetWindowLongPtr(handle, -20).ToInt64();
            // Layered-window click-through; decoration must not capture mouse or focus.
            Native.SetWindowLongPtr(handle, -20, new nint(style | 0x20 | 0x08000000 | 0x80));
        };
        SyncBounds();
    }
    public void SyncBounds()
    {
        if (phone.WindowState == WindowState.Minimized) { if (IsVisible) Hide(); return; }
        Left = phone.Left; Top = phone.Top;
        Width = Math.Max(1, phone.ActualWidth > 0 ? phone.ActualWidth : phone.Width);
        Height = Math.Max(1, phone.ActualHeight > 0 ? phone.ActualHeight : phone.Height);
        Topmost = phone.Topmost;
        if (phone.IsVisible && !IsVisible) Show();
    }
    public void SetIsland(bool visible)
    {
        if (artwork.DecorativeIsland == visible) return;
        artwork.DecorativeIsland = visible; artwork.InvalidateVisual();
    }
}

public sealed class FrameArtwork : FrameworkElement
{
    public bool DecorativeIsland { get; set; }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth < 32 || ActualHeight < 32) return;
        var frame = PhoneGeometry.Frame;
        var screenWidth = ActualWidth - frame * 2; var screenHeight = ActualHeight - frame * 2;
        var radius = PhoneGeometry.ScreenRadius(screenWidth, screenHeight);
        var outer = new RectangleGeometry(new Rect(0.5, 0.5, ActualWidth - 1, ActualHeight - 1), radius + frame - 0.5, radius + frame - 0.5);
        var inner = new RectangleGeometry(new Rect(frame, frame, screenWidth, screenHeight), radius, radius);
        var ring = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        var finish = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0.7),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromRgb(54, 58, 67), 0), new(Color.FromRgb(15, 17, 21), 0.16),
                new(Color.FromRgb(22, 24, 29), 0.7), new(Color.FromRgb(47, 51, 60), 1)
            }
        };
        context.DrawGeometry(finish, null, ring);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 128, 136, 151)), 0.65), outer);
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(3, 4, 7)), 0.6), inner);
        if (DecorativeIsland)
        {
            var pill = PhoneGeometry.Island(screenWidth, screenHeight); pill.Offset(frame, frame);
            var pillRadius = Math.Min(pill.Width, pill.Height) / 2;
            context.DrawRoundedRectangle(Brushes.Black, null, pill, pillRadius, pillRadius);
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(13, 22, 42)), null,
                new Point(pill.Right - Math.Min(pill.Width, pill.Height) / 2, pill.Top + pill.Height / 2), 3.2, 3.2);
        }
    }
}
