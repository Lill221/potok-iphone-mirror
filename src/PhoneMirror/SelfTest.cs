using System.IO;
namespace PhoneMirror;
public static class SelfTest
{
    public static void Run()
    {
        FrameRenderingTest.Run();
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var gpu = new Quality(1920, 1080, 60, true, true, true).Arguments;
        Check(gpu.Contains("-s 1920x1080@60 -fps 60"), "60fps advertisement missing");
        Check(gpu.Contains("-vd d3d11h264dec") && gpu.Contains("-vs d3d11videosink"), "GPU pipeline missing");
        var compatible = new Quality(1280, 720, 30, false, false, false).Arguments;
        Check(!compatible.Contains("d3d11") && compatible.Contains("-avdec ") && compatible.Contains("-as 0") && compatible.Contains("-vsync no"), "Compatibility must force software decoding");
        Check(Receiver.IsVideoFailure("streaming stopped, reason not-negotiated (-4)"), "Negotiation failure missed");
        Check(Receiver.IsVideoFailure("GStreamer error (video): Internal data stream error."), "Video failure missed");
        Check(!Receiver.IsVideoFailure("Initialized server socket(s)"), "Healthy startup mistaken for failure");
        const string connections = " TCP 172.20.10.3:54087 172.20.10.1:60285 ESTABLISHED 123\r\n TCP 127.0.0.1:123 127.0.0.1:5354 ESTABLISHED 123";
        Check(ConnectionInfo.HasUsbConnection(connections, 123, "172.20.10.3"), "USB route not detected");
        Check(!ConnectionInfo.HasUsbConnection(connections, 12, "172.20.10.3"), "PID substring false positive");
        Check(!ConnectionInfo.HasUsbConnection(connections, 123, "172.20.10.30"), "IP prefix false positive");
        Check(!ConnectionInfo.HasUsbConnection(connections.Replace("ESTABLISHED", "LISTENING"), 123, "172.20.10.3"), "Listener mistaken for stream");
        var lan = ConnectionInfo.FindRoute("TCP 192.0.2.7:5000 192.0.2.10:6000 ESTABLISHED 123", 123, [new("172.20.10.3", "USB", true), new("192.0.2.7", "LAN", false)]);
        Check(lan is { Usb: false }, "LAN connection not detected");
        Check(new Quality(2560, 1440, 60, false, true, true, true).Arguments.Contains("-h265"), "HEVC profile missing codec advertisement");
        var metrics = StreamMetrics.Parse("<plist><dict><key>encoderCurrentFPS</key><integer>60</integer><key>sentFramesAvg</key><integer>58</integer></dict></plist>", new());
        Check(metrics.EncoderFps == 60 && metrics.SentFps == 58, "iPhone metrics parsing failed");
        Check(StreamMetrics.FindSize("caps width=(int)498, height=(int)1080") == "498 × 1080", "Caps dimensions missing");
        Check(BleMouse.Pack(1, -5, 200, -1).SequenceEqual(new byte[] {1, 251, 127, 255}), "HID packet signed axes or clamping wrong");
        Check(Math.Abs(PhoneGeometry.Ratio("662 × 1440") - 662d/1440) < 0.00001, "Portrait stream aspect ratio wrong");
        Check(PhoneGeometry.Ratio("1440 × 662") > 2, "Landscape stream ratio wrong");
        var fit = PhoneGeometry.Fit(PhoneGeometry.IPhone17Ratio, 440, 900);
        Check(fit.Width <= 440 && fit.Height <= 900, "Phone overflows available area");
        var portraitIsland = PhoneGeometry.Island(350, 760);
        var landscapeIsland = PhoneGeometry.Island(760, 350);
        Check(portraitIsland.Width > portraitIsland.Height && portraitIsland.Top < 20, "Portrait island placement wrong");
        Check(landscapeIsland.Height > landscapeIsland.Width && landscapeIsland.Left < 20, "Landscape island placement wrong");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.passed.txt"), "Profile and USB route tests passed.");
    }
}
