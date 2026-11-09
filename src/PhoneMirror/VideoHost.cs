using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace PhoneMirror;

// Hosts the existing native renderer directly. No screen capture or frame copying.
public sealed class VideoHost : HwndHost
{
    private nint host, video, oldParent, oldStyle;
    private Native.Rect oldRect;
    private bool island;
    private int regionWidth, regionHeight;
    private bool? regionIsland;
    private bool smoothFrame;
    public bool SmoothFrame
    {
        get => smoothFrame;
        set { if (smoothFrame == value) return; smoothFrame = value; regionWidth = 0; UpdateRegion(); }
    }
    public bool Attached => video != 0 && Native.IsWindow(video);
    public bool Island { get => island; set { if (island == value) return; island = value; UpdateRegion(); } }
    public string? Error { get; private set; }

    protected override HandleRef BuildWindowCore(HandleRef parent)
    {
        host = Native.CreateWindowEx(0, "static", "PotokVideoHost", 0x40000000 | 0x02000000, 0, 0, 1, 1, parent.Handle, 0, 0, 0);
        if (host == 0) throw new System.ComponentModel.Win32Exception();
        regionWidth = regionHeight = 0; regionIsland = null;
        return new HandleRef(this, host);
    }
    protected override void DestroyWindowCore(HandleRef hwnd) { Detach(); Native.DestroyWindow(hwnd.Handle); host = 0; }
    protected override void OnWindowPositionChanged(Rect rcBoundingBox) { base.OnWindowPositionChanged(rcBoundingBox); ResizeVideo(); if (!Attached && host != 0) Native.ShowWindow(host, 0); }

    public void TryAttach(int? processId)
    {
        if (host == 0) return;
        if (Attached) { ResizeVideo(); Native.ShowWindow(host, 4); return; }
        Native.ShowWindow(host, 0);
        if (!processId.HasValue) return;
        video = 0;
        nint candidate = 0;
        Native.EnumWindows((window, _) =>
        {
            Native.GetWindowThreadProcessId(window, out var pid);
            if (pid != processId.Value || !Native.IsWindowVisible(window)) return true;
            var title = new StringBuilder(256); Native.GetWindowText(window, title, title.Capacity);
            // Only the renderer, never the receiver settings or another app.
            if (title.ToString().Contains("AirPlay Video Stream", StringComparison.OrdinalIgnoreCase)) { candidate = window; return false; }
            return true;
        }, 0);
        if (candidate == 0) return;
        oldParent = Native.GetParent(candidate); oldStyle = Native.GetWindowLongPtr(candidate, -16);
        Native.GetWindowRect(candidate, out oldRect);
        // Remove top-level frame, preserve renderer styles, and make it a child.
        Native.SetWindowLongPtr(candidate, -16, new nint((oldStyle.ToInt64() & ~0x80CF0000L) | 0x40000000L));
        Marshal.SetLastPInvokeError(0);
        Native.SetParent(candidate, host);
        if (Native.GetParent(candidate) != host)
        {
            Native.SetWindowLongPtr(candidate, -16, oldStyle);
            Error = "Не удалось встроить видео в рамку. Видео остаётся в отдельном окне.";
            return;
        }
        video = candidate; Error = null;
        ResizeVideo();
        Native.ShowWindow(host, 4);
    }
    private void ResizeVideo()
    {
        if (host == 0) return;
        Native.GetClientRect(host, out var rect);
        if (Attached)
        {
            Native.GetClientRect(video, out var videoRect);
            Native.GetWindowRect(video, out var positioned);
            var origin = new Native.Point(); Native.ClientToScreen(host, ref origin);
            // A renderer can resize itself when its format changes. Keep it fitted to the host.
            if (videoRect.Right != rect.Right || videoRect.Bottom != rect.Bottom || positioned.Left != origin.X || positioned.Top != origin.Y)
                Native.SetWindowPos(video, 0, 0, 0, Math.Max(1, rect.Right), Math.Max(1, rect.Bottom), 0x0034);
        }
        UpdateRegion();
    }
    private void UpdateRegion()
    {
        if (host == 0) return;
        Native.GetClientRect(host, out var rect);
        var width = rect.Right; var height = rect.Bottom;
        if (width < 2 || height < 2) return;
        if (regionWidth == width && regionHeight == height && regionIsland == island) return;
        var shortSide = Math.Min(width, height);
        // Leave video under the anti-aliased inner edge of the separate frame layer.
        var overlap = smoothFrame ? 4 * Native.GetDpiForWindow(host) / 96d : 0;
        var radius = (int)Math.Max(0, PhoneGeometry.ScreenRadius(width, height) - overlap);
        var region = Native.CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        if (island)
        {
            var geometry = PhoneGeometry.Island(width, height);
            var diameter = (int)Math.Min(geometry.Width, geometry.Height);
            var cutout = Native.CreateRoundRectRgn((int)geometry.X, (int)geometry.Y, (int)geometry.Right, (int)geometry.Bottom, diameter, diameter);
            Native.CombineRgn(region, region, cutout, 4); // DIFF: reveal the WPF island beneath the native surface.
            Native.DeleteObject(cutout);
        }
        if (Native.SetWindowRgn(host, region, true) == 0) Native.DeleteObject(region);
        else { regionWidth = width; regionHeight = height; regionIsland = island; }
    }
    public void Detach()
    {
        if (Attached)
        {
            Native.SetParent(video, oldParent);
            Native.SetWindowLongPtr(video, -16, oldStyle);
            Native.SetWindowPos(video, 0, oldRect.Left, oldRect.Top, oldRect.Right - oldRect.Left, oldRect.Bottom - oldRect.Top, 0x0034);
        }
        video = 0;
    }
}

internal static class Native
{
    public delegate bool EnumCallback(nint hwnd, nint lparam);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern nint CreateWindowEx(int ex, string cls, string name, int style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback, nint param);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] public static extern int GetWindowRgn(nint hwnd, nint region);
    [DllImport("user32.dll")] public static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetParent(nint hwnd, nint parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("gdi32.dll")] public static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] public static extern int CombineRgn(nint dest, nint source1, nint source2, int mode);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(nint handle);
    [DllImport("gdi32.dll")] public static extern bool PtInRegion(nint region, int x, int y);
}
