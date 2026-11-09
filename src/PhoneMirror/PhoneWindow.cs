using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Shell;

namespace PhoneMirror;
public sealed class PhoneWindow : Window
{
    private readonly Receiver receiver;
    private readonly VideoHost host = new();
    private readonly Grid screen = new() { Background = Brushes.Black };
    private readonly Border island = new() { Background = Brushes.Black, CornerRadius = new CornerRadius(20) };
    private readonly Canvas decoration = new() { IsHitTestVisible = false };
    private readonly Border body;
    private readonly TextBlock hint = new() { Text = "Выбери Potok\nв «Повторе экрана»", TextAlignment = TextAlignment.Center, FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(147, 158, 177)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private double ratio = PhoneGeometry.IPhone17Ratio;
    private bool fullscreen;
    private Rect beforeFullscreen;
    private string? lastSize;
    private bool wasAttached;
    private bool decorativeIsland;
    private PhoneFrameOverlay? smoothFrame;
    public PhoneWindow(Receiver receiver)
    {
        this.receiver = receiver;
        Title = "Поток · iPhone 17"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0, GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0),
            ResizeBorderThickness = new Thickness(6), UseAeroCaptionButtons = false
        });
        Background = Brushes.Black; MinWidth = 180; MinHeight = 180; ShowInTaskbar = true;
        var workArea = SystemParameters.WorkArea;
        var fit = PhoneGeometry.Fit(ratio, Math.Min(440, workArea.Width * 0.45), workArea.Height * 0.84);
        Width = fit.Width; Height = fit.Height; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        body = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(7), CornerRadius = new CornerRadius(49), Background = new SolidColorBrush(Color.FromRgb(12, 13, 16)), BorderBrush = new LinearGradientBrush(Color.FromRgb(68, 73, 83), Color.FromRgb(25, 27, 33), 35) };
        var glass = new Border { Background = Brushes.Black, Child = screen };
        body.Child = glass; Content = body;
        screen.Children.Add(hint);
        island.Child = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(13, 22, 42)), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
        decoration.Children.Add(island); screen.Children.Add(decoration); screen.Children.Add(host);
        screen.SizeChanged += (_, _) => UpdateShape();
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WindowMessage);
            UpdateShape();
        };
        Loaded += (_, _) => { smoothFrame = new PhoneFrameOverlay(this); smoothFrame.SyncBounds(); UpdateShape(); UpdateIsland(); };
        LocationChanged += (_, _) => smoothFrame?.SyncBounds();
        StateChanged += (_, _) => smoothFrame?.SyncBounds();
        Activated += (_, _) => smoothFrame?.SyncBounds();
        SizeChanged += (_, _) => { UpdateShape(); smoothFrame?.SyncBounds(); };
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) ToggleFullscreen(); else if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape && fullscreen) ToggleFullscreen(); else if (e.Key == Key.Escape) Close(); else if (e.Key == Key.F11) ToggleFullscreen(); };
        var menu = new ContextMenu();
        var dynamicIsland = new MenuItem { Header = "Добавить декоративный Dynamic Island", IsCheckable = true, IsChecked = false };
        dynamicIsland.Click += (_, _) => { decorativeIsland = dynamicIsland.IsChecked; UpdateIsland(); };
        menu.Items.Add(dynamicIsland);
        var topmost = new MenuItem { Header = "Поверх других окон", IsCheckable = true };
        topmost.Click += (_, _) => { Topmost = topmost.IsChecked; smoothFrame?.SyncBounds(); }; menu.Items.Add(topmost);
        var full = new MenuItem { Header = "Полный экран / F11" }; full.Click += (_, _) => ToggleFullscreen(); menu.Items.Add(full);
        var close = new MenuItem { Header = "Закрыть окно / Esc" }; close.Click += (_, _) => Close(); menu.Items.Add(close);
        ContextMenu = menu;
        timer.Tick += (_, _) =>
        {
            host.TryAttach(receiver.ProcessId);
            if (host.Attached != wasAttached)
            {
                wasAttached = host.Attached;
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(receiver.LogDirectory, "phone-window.log"), $"{DateTime.Now:o} embedded={wasAttached} process={receiver.ProcessId}{Environment.NewLine}"); } catch (System.IO.IOException) { }
            }
            hint.Visibility = host.Attached ? Visibility.Collapsed : Visibility.Visible;
            UpdateIsland();
            if (host.Error != null) hint.Text = host.Error;
            var size = receiver.Metrics.Size;
            if (size != null && size != lastSize)
            {
                var previousRatio = ratio;
                lastSize = size; ratio = PhoneGeometry.Ratio(size);
                if (!fullscreen)
                {
                    var bounds = SystemParameters.WorkArea;
                    var changedOrientation = (previousRatio > 1) != (ratio > 1);
                    var availableHeight = changedOrientation ? bounds.Height * 0.84 : Math.Min(Height, bounds.Height * 0.84);
                    var fitted = PhoneGeometry.Fit(ratio, Math.Min(bounds.Width - 60, ratio > 1 ? 980 : 440), availableHeight);
                    Width = fitted.Width; Height = fitted.Height;
                    Left = Math.Clamp(Left, bounds.Left, Math.Max(bounds.Left, bounds.Right - Width));
                    Top = Math.Clamp(Top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - Height));
                }
                UpdateShape();
            }
            Title = receiver.DeviceModel == "iPhone18,3" ? "Поток · iPhone 17" : receiver.DeviceModel != null ? $"Поток · {receiver.DeviceModel}" : "Поток · iPhone";
        };
        timer.Start();
        Closed += (_, _) => { timer.Stop(); smoothFrame?.Close(); smoothFrame = null; host.Detach(); host.Dispose(); };
    }

    private void UpdateShape()
    {
        var width = screen.ActualWidth; var height = screen.ActualHeight;
        if (width <= 0 || height <= 0) return;
        var radius = PhoneGeometry.ScreenRadius(width, height);
        body.CornerRadius = new CornerRadius(radius + PhoneGeometry.Frame);
        screen.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        var pill = PhoneGeometry.Island(width, height);
        island.Width = pill.Width; island.Height = pill.Height;
        Canvas.SetLeft(island, pill.X); Canvas.SetTop(island, pill.Y);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var outerRadius = (radius + PhoneGeometry.Frame) * dpi.DpiScaleX;
        Native.GetWindowRect(handle, out var windowRect);
        // The binary native edge is hidden underneath the fully opaque part of the vector ring.
        // Its visible outer edge is drawn by the separate per-pixel-alpha window.
        var inset = (int)Math.Ceiling(2 * dpi.DpiScaleX);
        var region = Native.CreateRoundRectRgn(inset, inset, windowRect.Right - windowRect.Left - inset + 1, windowRect.Bottom - windowRect.Top - inset + 1, (int)((outerRadius - inset) * 2), (int)((outerRadius - inset) * 2));
        if (Native.SetWindowRgn(handle, region, true) == 0) Native.DeleteObject(region);
    }
    private void UpdateIsland()
    {
        // The real iPhone island (including its live status) is already in this device's stream.
        // Only draw a placeholder while waiting, or an explicitly requested decorative overlay.
        host.Island = false;
        host.SmoothFrame = smoothFrame != null;
        island.Visibility = Visibility.Collapsed;
        smoothFrame?.SetIsland(!host.Attached || decorativeIsland);
    }
    private nint WindowMessage(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x0084 && !fullscreen) // WM_NCHITTEST: resize from the physical frame.
        {
            var x = (short)(lparam.ToInt64() & 0xffff); var y = (short)((lparam.ToInt64() >> 16) & 0xffff);
            Native.GetWindowRect(hwnd, out var bounds);
            var left = x - bounds.Left < 7; var right = bounds.Right - x < 7; var top = y - bounds.Top < 7; var bottom = bounds.Bottom - y < 7;
            var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
            if (hit != 0) { handled = true; return hit; }
        }
        if (message == 0x0214 && !fullscreen) // WM_SIZING: preserve the stream's aspect ratio.
        {
            var rect = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.Rect>(lparam);
            var edge = wparam.ToInt32();
            var frame = 16 * VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (edge is 3 or 6) rect.Right = rect.Left + (int)((rect.Bottom - rect.Top - frame) * ratio + frame);
            else if (edge is 1 or 2 or 4 or 5 or 7 or 8)
            {
                var height = (int)((rect.Right - rect.Left - frame) / ratio + frame);
                if (edge is 4 or 5) rect.Top = rect.Bottom - height; else rect.Bottom = rect.Top + height;
            }
            System.Runtime.InteropServices.Marshal.StructureToPtr(rect, lparam, false); handled = true; return 1;
        }
        return 0;
    }
    private void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            beforeFullscreen = new Rect(Left, Top, Width, Height);
            var workArea = SystemParameters.WorkArea;
            var fit = PhoneGeometry.Fit(ratio, workArea.Width, workArea.Height);
            Left = workArea.Left + (workArea.Width - fit.Width) / 2; Top = workArea.Top;
            Width = fit.Width; Height = fit.Height; fullscreen = true;
        }
        else { fullscreen = false; Left = beforeFullscreen.Left; Top = beforeFullscreen.Top; Width = beforeFullscreen.Width; Height = beforeFullscreen.Height; }
    }
}
