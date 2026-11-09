using System.IO;
using System.Windows;
namespace PhoneMirror;
public static class NativeHostTest
{
    public static async Task Run()
    {
        var host = new VideoHost();
        var window = new Window { Width = 350, Height = 700, Content = host, Title = "Поток · проверка встраивания" };
        window.Show();
        await Task.Delay(250);
        var video = Native.CreateWindowEx(0, "static", "AirPlay Video Stream (integration test)", unchecked((int)0x10CF0000), 0, 0, 200, 400, 0, 0, 0, 0);
        try
        {
            if (video == 0) throw new Exception("Test renderer creation failed");
            host.TryAttach(Environment.ProcessId);
            if (!host.Attached || Native.GetParent(video) == 0) throw new Exception("Native video attachment failed: " + host.Error);
            window.Width = 500; window.Height = 600;
            await Task.Delay(150);
            host.Island = false; host.Island = true;
            var parent = Native.GetParent(video);
            Native.GetClientRect(parent, out var surface);
            Native.SetWindowPos(video, 0, 30, 40, 100, 200, 0x0034);
            host.TryAttach(Environment.ProcessId);
            Native.GetClientRect(video, out var repairedSize);
            Native.GetWindowRect(video, out var repairedPosition);
            var expectedOrigin = new Native.Point(); Native.ClientToScreen(parent, ref expectedOrigin);
            if (repairedSize.Right != surface.Right || repairedSize.Bottom != surface.Bottom || repairedPosition.Left != expectedOrigin.X || repairedPosition.Top != expectedOrigin.Y)
                throw new Exception("Renderer auto-resize was not corrected");
            var pill = PhoneGeometry.Island(surface.Right, surface.Bottom);
            var region = Native.CreateRoundRectRgn(0, 0, 1, 1, 0, 0);
            try
            {
                if (Native.GetWindowRgn(parent, region) == 0) throw new Exception("Missing rounded clip region");
                if (Native.PtInRegion(region, 0, 0)) throw new Exception("Corner was not clipped");
                if (!Native.PtInRegion(region, surface.Right / 2, surface.Bottom / 2)) throw new Exception("Video center was clipped");
                var pillX = (int)(pill.X + pill.Width / 2); var pillY = (int)(pill.Y + pill.Height / 2);
                if (Native.PtInRegion(region, pillX, pillY)) throw new Exception("Dynamic Island was not masked");
                host.Island = false; Native.GetWindowRgn(parent, region);
                if (!Native.PtInRegion(region, pillX, pillY)) throw new Exception("Island toggle did not restore video");
            }
            finally { Native.DeleteObject(region); }
            host.Detach();
            if (Native.GetParent(video) != 0) throw new Exception("Renderer was not detached");
            using var receiver = new Receiver();
            var phone = new PhoneWindow(receiver);
            try
            {
                phone.Show(); await Task.Delay(300);
                var phoneHandle = new System.Windows.Interop.WindowInteropHelper(phone).Handle;
                Native.GetWindowRect(phoneHandle, out var outer); Native.GetClientRect(phoneHandle, out var client);
                if (Math.Abs(outer.Right - outer.Left - client.Right) > 1 || Math.Abs(outer.Bottom - outer.Top - client.Bottom) > 1)
                    throw new Exception($"Native non-client border still present: outer {outer.Right - outer.Left}x{outer.Bottom - outer.Top}, client {client.Right}x{client.Bottom}");
                var aspect = (phone.ActualWidth - 16) / (phone.ActualHeight - 16);
                if (Math.Abs(aspect - PhoneGeometry.IPhone17Ratio) > 0.005) throw new Exception("Phone frame aspect mismatch");
                var overlay = phone.OwnedWindows.OfType<PhoneFrameOverlay>().SingleOrDefault();
                if (overlay == null || !overlay.AllowsTransparency || !overlay.IsVisible) throw new Exception("Alpha frame layer is missing");
                var overlayHandle = new System.Windows.Interop.WindowInteropHelper(overlay).Handle;
                var overlayStyle = Native.GetWindowLongPtr(overlayHandle, -20).ToInt64();
                if ((overlayStyle & (0x20 | 0x80000 | 0x08000000)) != (0x20 | 0x80000 | 0x08000000))
                    throw new Exception("Frame layer is not layered, click-through, and non-activating");
                if (Math.Abs(overlay.ActualWidth - phone.ActualWidth) > 1 || Math.Abs(overlay.ActualHeight - phone.ActualHeight) > 1)
                    throw new Exception("Frame layer does not match phone bounds");
            }
            finally { phone.Close(); }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "native-host-test.passed.txt"), "Attach, resize correction, clipping, detach, borderless geometry, alpha frame overlay/click-through passed. Synthetic renderer; live appearance requires confirmation.");
        }
        finally { if (video != 0) Native.DestroyWindow(video); window.Close(); }
    }
}
